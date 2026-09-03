using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using WolfLive.Api;
using WolfLive.Api.Models;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;

namespace PenaltyBot
{
    public class PenaltyPlayer
    {
        public string UserId { get; set; } = "";
        public string Name { get; set; } = "";
        public int Number { get; set; }

        public int Shots { get; set; }
        public int Goals { get; set; }

        public bool Eliminated { get; set; }
    }

    public class PenaltyGame
    {
        public string GroupId { get; set; } = "";

        public List<PenaltyPlayer> Players { get; set; } = new();

        public int CurrentPlayerIndex { get; set; }

        public bool Started { get; set; }

        public bool TurnAnswered { get; set; }

        public long TurnId { get; set; }

        public CancellationTokenSource? TurnCancellation { get; set; }
    }

    public static class Program
    {
        private static IWolfClient? _client;

        private static readonly Dictionary<string, PenaltyGame> Games = new();

        private static readonly object GameLock = new();

        private static readonly Random Random = new();

        private const int MaxPlayers = 10;
        private const int MinPlayers = 2;

        private const int ShotsPerPlayer = 5;

        private const int TurnSeconds = 25;

        public static async Task Main(string[] args)
        {
            try
            {
                Console.WriteLine("======================================");
                Console.WriteLine("       PENALTY BOT - WOLFLIVE");
                Console.WriteLine("======================================");

                _client = new WolfClient();

                Console.WriteLine("البوت يحاول الاتصال...");

                await ConnectBot();

                Console.WriteLine("البوت متصل بنجاح.");

                await Task.Delay(Timeout.Infinite);
            }
            catch (Exception ex)
            {
                Console.WriteLine("خطأ رئيسي:");
                Console.WriteLine(ex);

                await Task.Delay(5000);
            }
        }

        private static async Task ConnectBot()
        {
            if (_client == null)
                return;

            var clientType = _client.GetType();

            Console.WriteLine("WolfLive Client: " + clientType.FullName);

            var connectMethod = clientType
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(x =>
                    x.Name.Equals("Connect", StringComparison.OrdinalIgnoreCase));

            if (connectMethod != null)
            {
                var parameters = connectMethod.GetParameters();

                if (parameters.Length == 0)
                {
                    var result = connectMethod.Invoke(_client, null);

                    if (result is Task task)
                        await task;
                }
            }

            RegisterMessageHandler();
        }

        private static void RegisterMessageHandler()
        {
            if (_client == null)
                return;

            try
            {
                var clientType = _client.GetType();

                var events = clientType
                    .GetEvents(BindingFlags.Public | BindingFlags.Instance);

                foreach (var ev in events)
                {
                    if (ev.Name.Contains("Message", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine(
                            "تم العثور على حدث الرسائل: " + ev.Name);

                        try
                        {
                            var handler = Delegate.CreateDelegate(
                                ev.EventHandlerType!,
                                typeof(Program)
                                    .GetMethod(
                                        nameof(GenericMessageHandler),
                                        BindingFlags.NonPublic |
                                        BindingFlags.Static)!
                            );

                            ev.AddEventHandler(_client, handler);

                            Console.WriteLine(
                                "تم تسجيل استقبال الرسائل.");

                            return;
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "خطأ تسجيل الرسائل: " + ex.Message);
            }
        }

        private static void GenericMessageHandler(object? sender, object? message)
        {
            try
            {
                if (message == null)
                    return;

                _ = HandleMessageObject(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "Message Handler Error: " + ex.Message);
            }
        }

        private static async Task HandleMessageObject(object message)
        {
            string text = GetMessageText(message);

            if (string.IsNullOrWhiteSpace(text))
                return;

            string groupId = GetGroupId(message);

            if (string.IsNullOrWhiteSpace(groupId))
                return;

            string userId = GetUserId(message);

            string userName = GetUserName(message);

            Console.WriteLine(
                $"[{groupId}] {userName}: {text}");

            await ProcessCommand(
                groupId,
                userId,
                userName,
                text);
        }

        private static async Task ProcessCommand(
            string groupId,
            string userId,
            string userName,
            string text)
        {
            text = text.Trim();

            if (text.Equals("!جزاء مساعدة",
                StringComparison.OrdinalIgnoreCase))
            {
                await SendMessage(
                    groupId,
                    "⚽ لعبة الجزاء\n\n" +
                    "!جزاء انضم — الانضمام للعبة\n" +
                    "!جزاء لاعبين — عرض اللاعبين\n" +
                    "!جزاء بدء — بدء المباراة\n" +
                    "!جزاء حالة — حالة المباراة\n" +
                    "!جزاء انهاء — إنهاء المباراة\n\n" +
                    "بعد بدء المباراة:\n" +
                    "1 = يسار\n" +
                    "2 = وسط\n" +
                    "3 = يمين\n\n" +
                    "⏱️ لديك 25 ثانية لكل تسديدة\n" +
                    "⚽ لكل لاعب 5 تسديدات"
                );

                return;
            }

            if (text.Equals("!جزاء انضم",
                StringComparison.OrdinalIgnoreCase))
            {
                await JoinGame(
                    groupId,
                    userId,
                    userName);

                return;
            }

            if (text.Equals("!جزاء لاعبين",
                StringComparison.OrdinalIgnoreCase))
            {
                await ShowPlayers(groupId);
                return;
            }

            if (text.Equals("!جزاء بدء",
                StringComparison.OrdinalIgnoreCase))
            {
                await StartGame(groupId);
                return;
            }

            if (text.Equals("!جزاء حالة",
                StringComparison.OrdinalIgnoreCase))
            {
                await ShowStatus(groupId);
                return;
            }

            if (text.Equals("!جزاء انهاء",
                StringComparison.OrdinalIgnoreCase))
            {
                await EndGame(groupId);
                return;
            }

            if (text == "1")
            {
                await ProcessShot(groupId, userId, 1);
                return;
            }

            if (text == "2")
            {
                await ProcessShot(groupId, userId, 2);
                return;
            }

            if (text == "3")
            {
                await ProcessShot(groupId, userId, 3);
                return;
            }
        }

        private static async Task JoinGame(
            string groupId,
            string userId,
            string userName)
        {
            if (string.IsNullOrWhiteSpace(userId))
                userId = userName;

            if (string.IsNullOrWhiteSpace(userName))
                userName = "لاعب";

            PenaltyGame game;

            lock (GameLock)
            {
                if (!Games.TryGetValue(groupId, out game!))
                {
                    game = new PenaltyGame
                    {
                        GroupId = groupId
                    };

                    Games[groupId] = game;
                }

                if (game.Started)
                {
                    _ = SendMessage(
                        groupId,
                        "⚠️ اللعبة بدأت بالفعل، لا يمكن الانضمام الآن."
                    );

                    return;
                }

                if (game.Players.Any(x => x.UserId == userId))
                {
                    _ = SendMessage(
                        groupId,
                        $"⚠️ {userName} أنت مسجل باللعبة مسبقاً."
                    );

                    return;
                }

                if (game.Players.Count >= MaxPlayers)
                {
                    _ = SendMessage(
                        groupId,
                        "❌ اكتمل عدد اللاعبين. الحد الأقصى 10 لاعبين."
                    );

                    return;
                }

                var player = new PenaltyPlayer
                {
                    UserId = userId,
                    Name = userName,
                    Number = game.Players.Count + 1
                };

                game.Players.Add(player);

                _ = SendMessage(
                    groupId,
                    $"✅ {userName} انضم للعبة الجزاء!\n" +
                    $"👥 عدد اللاعبين: {game.Players.Count}/{MaxPlayers}\n\n" +
                    "اكتب !جزاء بدء عندما يكتمل اللاعبون."
                );
            }

            await Task.CompletedTask;
        }

        private static async Task ShowPlayers(string groupId)
        {
            PenaltyGame? game;

            lock (GameLock)
            {
                Games.TryGetValue(groupId, out game);
            }

            if (game == null || game.Players.Count == 0)
            {
                await SendMessage(
                    groupId,
                    "⚠️ لا يوجد لاعبون مسجلون حالياً."
                );

                return;
            }

            List<PenaltyPlayer> players;

            lock (GameLock)
            {
                players = game.Players
                    .Select(x => new PenaltyPlayer
                    {
                        UserId = x.UserId,
                        Name = x.Name,
                        Number = x.Number,
                        Shots = x.Shots,
                        Goals = x.Goals,
                        Eliminated = x.Eliminated
                    })
                    .ToList();
            }

            var lines = new List<string>
            {
                "⚽ لاعبو لعبة الجزاء:",
                ""
            };

            foreach (var player in players)
            {
                lines.Add($"{player.Number}. {player.Name}");
            }

            await SendMessage(
                groupId,
                string.Join("\n", lines));
        }

        private static async Task StartGame(string groupId)
        {
            PenaltyGame? game;

            lock (GameLock)
            {
                if (!Games.TryGetValue(groupId, out game))
                    game = null;

                if (game == null)
                {
                    _ = SendMessage(
                        groupId,
                        "❌ لا توجد لعبة. استخدم !جزاء انضم أولاً."
                    );

                    return;
                }

                if (game.Started)
                {
                    _ = SendMessage(
                        groupId,
                        "⚠️ اللعبة بدأت بالفعل."
                    );

                    return;
                }

                if (game.Players.Count < MinPlayers)
                {
                    _ = SendMessage(
                        groupId,
                        $"❌ تحتاج اللعبة إلى {MinPlayers} لاعبين على الأقل."
                    );

                    return;
                }

                game.Started = true;
                game.CurrentPlayerIndex = 0;
                game.TurnAnswered = false;
                game.TurnId = 0;

                foreach (var player in game.Players)
                {
                    player.Shots = 0;
                    player.Goals = 0;
                    player.Eliminated = false;
                }
            }

            await SendMessage(
                groupId,
                "🏆 بدأت لعبة الجزاء!\n\n" +
                $"👥 عدد اللاعبين: {game.Players.Count}\n" +
                "⚽ لكل لاعب 5 تسديدات\n" +
                "⏱️ لديك 25 ثانية لكل تسديدة\n\n" +
                "1️⃣ يسار\n" +
                "2️⃣ وسط\n" +
                "3️⃣ يمين"
            );

            await Task.Delay(1000);

            await StartTurn(groupId);
        }

        private static async Task StartTurn(string groupId)
        {
            PenaltyGame? game;
            PenaltyPlayer? player;
            long turnId;
            CancellationTokenSource cts;

            lock (GameLock)
            {
                if (!Games.TryGetValue(groupId, out game))
                    return;

                if (!game.Started)
                    return;

                if (game.Players.Count == 0)
                    return;

                int foundIndex = -1;

                for (int i = 0; i < game.Players.Count; i++)
                {
                    int index =
                        (game.CurrentPlayerIndex + i)
                        % game.Players.Count;

                    if (game.Players[index].Shots < ShotsPerPlayer)
                    {
                        foundIndex = index;
                        break;
                    }
                }

                if (foundIndex == -1)
                {
                    game.Started = false;
                    _ = FinishGame(groupId);
                    return;
                }

                game.CurrentPlayerIndex = foundIndex;
                player = game.Players[foundIndex];
                game.TurnAnswered = false;
                game.TurnId++;
                turnId = game.TurnId;

                try
                {
                    game.TurnCancellation?.Cancel();
                }
                catch
                {
                }

                cts = new CancellationTokenSource();
                game.TurnCancellation = cts;
            }

            int remainingShots =
                ShotsPerPlayer - player.Shots;

            await SendMessage(
                groupId,
                $"⚽ دور اللاعب: {player.Name}\n\n" +
                $"🎯 التسديدة: {player.Shots + 1}/{ShotsPerPlayer}\n" +
                $"⚽ الأهداف: {player.Goals}\n" +
                $"📊 المتبقي لك: {remainingShots} تسديدة\n\n" +
                "اختار اتجاه التسديدة:\n" +
                "1️⃣ يسار\n" +
                "2️⃣ وسط\n" +
                "3️⃣ يمين\n\n" +
                "⏱️ لديك 25 ثانية!"
            );

            _ = StartTurnTimeout(
                groupId,
                player.UserId,
                turnId,
                cts.Token);
        }

        private static async Task StartTurnTimeout(
            string groupId,
            string userId,
            long turnId,
            CancellationToken token)
        {
            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(TurnSeconds),
                    token);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            await TimeoutPlayer(
                groupId,
                userId,
                turnId);
        }

        private static async Task TimeoutPlayer(
            string groupId,
            string userId,
            long turnId)
        {
            string playerName = "";
            bool shouldFinish = false;
            bool shouldContinue = false;

            lock (GameLock)
            {
                if (!Games.TryGetValue(groupId, out var game))
                    return;

                if (!game.Started)
                    return;

                if (game.TurnId != turnId)
                    return;

                if (game.TurnAnswered)
                    return;

                int index = game.Players.FindIndex(
                    x => x.UserId == userId);

                if (index < 0)
                    return;

                playerName = game.Players[index].Name;
                game.TurnAnswered = true;
                game.Players[index].Eliminated = true;

                game.Players.RemoveAt(index);

                if (game.Players.Count == 0)
                {
                    game.Started = false;
                    Games.Remove(groupId);
                }
                else
                {
                    if (index < game.CurrentPlayerIndex)
                        game.CurrentPlayerIndex--;

                    if (game.CurrentPlayerIndex >= game.Players.Count)
                        game.CurrentPlayerIndex = 0;

                    shouldFinish =
                        game.Players.All(
                            x => x.Shots >= ShotsPerPlayer);

                    if (!shouldFinish)
                        shouldContinue = true;
                }

                try
                {
                    game.TurnCancellation?.Cancel();
                }
                catch
                {
                }
            }

            await SendMessage(
                groupId,
                $"⏰ انتهى الوقت!\n\n" +
                $"❌ اللاعب {playerName} لم يسدد خلال 25 ثانية.\n" +
                "🚫 تم حذفه من لعبة الجزاء فقط.\n" +
                "ℹ️ لم يتم طرده من الروم."
            );

            if (shouldFinish)
            {
                await FinishGame(groupId);
                return;
            }

            if (shouldContinue)
            {
                await Task.Delay(800);
                await StartTurn(groupId);
            }
        }

        private static async Task ProcessShot(
            string groupId,
            string userId,
            int direction)
        {
            PenaltyGame? game;
            PenaltyPlayer? player;
            long turnId;

            lock (GameLock)
            {
                if (!Games.TryGetValue(groupId, out game))
                    return;

                if (!game.Started)
                    return;

                if (game.Players.Count == 0)
                    return;

                player =
                    game.Players[game.CurrentPlayerIndex];

                if (player.UserId != userId)
                    return;

                if (game.TurnAnswered)
                    return;

                turnId = game.TurnId;
            }

            int keeperDirection;

            lock (Random)
            {
                keeperDirection = Random.Next(1, 4);
            }

            bool goal = direction != keeperDirection;

            int shotNumber;

            lock (GameLock)
            {
                if (!Games.TryGetValue(groupId, out game))
                    return;

                if (!game.Started)
                    return;

                if (game.TurnId != turnId)
                    return;

                if (game.TurnAnswered)
                    return;

                if (game.Players.Count == 0)
                    return;

                var current =
                    game.Players[game.CurrentPlayerIndex];

                if (current.UserId != userId)
                    return;

                game.TurnAnswered = true;
                current.Shots++;
                shotNumber = current.Shots;

                if (goal)
                    current.Goals++;

                try
                {
                    game.TurnCancellation?.Cancel();
                }
                catch
                {
                }

                player = current;
            }

            string directionName =
                GetDirectionName(direction);

            string keeperName =
                GetDirectionName(keeperDirection);

            if (goal)
            {
                await SendMessage(
                    groupId,
                    $"⚽🔥 GOAL!\n\n" +
                    $"👤 اللاعب: {player.Name}\n" +
                    $"🎯 التسديدة: {shotNumber}/{ShotsPerPlayer}\n" +
                    $"➡️ الاتجاه: {directionName}\n" +
                    $"🧤 الحارس: {keeperName}\n\n" +
                    $"🥅 هدف! مبروك!\n" +
                    $"📊 أهدافك: {player.Goals}"
                );
            }
            else
            {
                await SendMessage(
                    groupId,
                    $"🧤❌ SAVE!\n\n" +
                    $"👤 اللاعب: {player.Name}\n" +
                    $"🎯 التسديدة: {shotNumber}/{ShotsPerPlayer}\n" +
                    $"➡️ الاتجاه: {directionName}\n" +
                    $"🧤 الحارس: {keeperName}\n\n" +
                    "❌ الحارس تصدى للكرة!"
                );
            }

            try
            {
                byte[] imageBytes =
                    CreatePenaltyImage(
                        player,
                        direction,
                        keeperDirection,
                        goal);

                Console.WriteLine(
                    "IMAGE BYTES: " + imageBytes.Length);

                await SendImage(
                    groupId,
                    imageBytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "Image Error: " + ex);
            }

            await Task.Delay(700);

            bool finished = false;
            bool nextTurn = false;

            lock (GameLock)
            {
                if (!Games.TryGetValue(groupId, out game))
                    return;

                if (!game.Started)
                    return;

                if (game.Players.Count > 0 &&
                    game.Players.All(
                        x => x.Shots >= ShotsPerPlayer))
                {
                    finished = true;
                }
                else
                {
                    var current =
                        game.Players[game.CurrentPlayerIndex];

                    if (current.Shots >= ShotsPerPlayer)
                    {
                        nextTurn = true;

                        int oldIndex =
                            game.CurrentPlayerIndex;

                        int count =
                            game.Players.Count;

                        for (int i = 1; i <= count; i++)
                        {
                            int nextIndex =
                                (oldIndex + i) % count;

                            if (game.Players[nextIndex].Shots <
                                ShotsPerPlayer)
                            {
                                game.CurrentPlayerIndex =
                                    nextIndex;

                                break;
                            }
                        }
                    }
                    else
                    {
                        nextTurn = true;
                    }
                }
            }

            if (finished)
            {
                await FinishGame(groupId);
                return;
            }

            if (nextTurn)
                await StartTurn(groupId);
        }

        private static string GetDirectionName(int direction)
        {
            return direction switch
            {
                1 => "يسار",
                2 => "وسط",
                3 => "يمين",
                _ => "غير معروف"
            };
        }

        private static async Task ShowStatus(string groupId)
        {
            PenaltyGame? game;

            lock (GameLock)
            {
                Games.TryGetValue(groupId, out game);
            }

            if (game == null)
            {
                await SendMessage(
                    groupId,
                    "⚠️ لا توجد لعبة حالياً."
                );

                return;
            }

            string message;

            lock (GameLock)
            {
                if (game.Players.Count == 0)
                {
                    message = "⚠️ لا يوجد لاعبون.";
                }
                else
                {
                    var lines =
                        new List<string>();

                    lines.Add(
                        game.Started
                            ? "🏆 اللعبة جارية"
                            : "⏸️ اللعبة غير مبدوءة");

                    lines.Add("");
                    lines.Add(
                        $"👥 اللاعبون: {game.Players.Count}");
                    lines.Add("");

                    for (int i = 0;
                         i < game.Players.Count;
                         i++)
                    {
                        var p =
                            game.Players[i];

                        string current =
                            game.Started &&
                            i == game.CurrentPlayerIndex
                                ? " 👈"
                                : "";

                        lines.Add(
                            $"{p.Number}. {p.Name} — " +
                            $"{p.Goals} هدف / {p.Shots} تسديدات" +
                            current);
                    }

                    message =
                        string.Join("\n", lines);
                }
            }

            await SendMessage(
                groupId,
                message);
        }

        private static async Task EndGame(string groupId)
        {
            PenaltyGame? game;

            lock (GameLock)
            {
                if (!Games.TryGetValue(groupId, out game))
                {
                    game = null;
                }
                else
                {
                    game.Started = false;

                    try
                    {
                        game.TurnCancellation?.Cancel();
                    }
                    catch
                    {
                    }

                    Games.Remove(groupId);
                }
            }

            if (game == null)
            {
                await SendMessage(
                    groupId,
                    "⚠️ لا توجد لعبة لإنهائها."
                );

                return;
            }

            await SendMessage(
                groupId,
                "🛑 تم إنهاء لعبة الجزاء."
            );
        }

        private static async Task FinishGame(string groupId)
        {
            List<PenaltyPlayer> ranking;

            lock (GameLock)
            {
                if (!Games.TryGetValue(
                    groupId,
                    out var game))
                {
                    return;
                }

                game.Started = false;

                try
                {
                    game.TurnCancellation?.Cancel();
                }
                catch
                {
                }

                ranking =
                    game.Players
                        .OrderByDescending(
                            x => x.Goals)
                        .ThenByDescending(
                            x => x.Shots)
                        .Select(x =>
                            new PenaltyPlayer
                            {
                                UserId = x.UserId,
                                Name = x.Name,
                                Number = x.Number,
                                Shots = x.Shots,
                                Goals = x.Goals
                            })
                        .ToList();

                Games.Remove(groupId);
            }

            if (ranking.Count == 0)
            {
                await SendMessage(
                    groupId,
                    "🏁 انتهت اللعبة.\nلا يوجد لاعبون."
                );

                return;
            }

            var lines =
                new List<string>();

            lines.Add(
                "🏆🏆 انتهت لعبة الجزاء! 🏆🏆");

            lines.Add("");
            lines.Add("📊 النتائج النهائية:");
            lines.Add("");

            for (int i = 0;
                 i < ranking.Count;
                 i++)
            {
                var p =
                    ranking[i];

                string medal =
                    i switch
                    {
                        0 => "🥇",
                        1 => "🥈",
                        2 => "🥉",
                        _ => "🏅"
                    };

                lines.Add(
                    $"{medal} {i + 1}. {p.Name}\n" +
                    $"   ⚽ {p.Goals} أهداف / " +
                    $"{p.Shots} تسديدات");
            }

            lines.Add("");
            lines.Add(
                "🎮 لإنشاء لعبة جديدة استخدم:");
            lines.Add("!جزاء انضم");

            await SendMessage(
                groupId,
                string.Join("\n", lines));
        }

        private static byte[] CreatePenaltyImage(
            PenaltyPlayer player,
            int shotDirection,
            int keeperDirection,
            bool goal)
        {
            const int width = 1000;
            const int height = 650;

            using var image =
                new Image<Rgba32>(
                    width,
                    height);

            FillRect(
                image,
                0, 0,
                width, height,
                new Rgba32(20, 30, 45));

            FillRect(
                image,
                0, 0,
                width, 300,
                new Rgba32(80, 150, 210));

            FillRect(
                image,
                0, 280,
                width, 370,
                new Rgba32(40, 145, 65));

            DrawRect(
                image,
                40, 300,
                920, 300,
                4,
                new Rgba32(245, 245, 245));

            DrawRect(
                image,
                220, 330,
                560, 250,
                5,
                new Rgba32(245, 245, 245));

            FillCircle(
                image,
                500, 520,
                8,
                new Rgba32(255, 255, 255));

            DrawGoal(image);

            int keeperX =
                keeperDirection switch
                {
                    1 => 390,
                    2 => 500,
                    3 => 610,
                    _ => 500
                };

            DrawKeeper(
                image,
                keeperX,
                240);

            int ballX =
                shotDirection switch
                {
                    1 => 385,
                    2 => 500,
                    3 => 615,
                    _ => 500
                };

            int ballY = 155;

            FillCircle(
                image,
                ballX,
                ballY,
                24,
                new Rgba32(250, 250, 250));

            DrawCircle(
                image,
                ballX,
                ballY,
                24,
                4,
                new Rgba32(20, 20, 20));

            if (goal)
                DrawGoalEffect(image, ballX, ballY);
            else
                DrawSaveEffect(image, keeperX, 240);

            Rgba32 bannerColor =
                goal
                    ? new Rgba32(20, 170, 70)
                    : new Rgba32(190, 40, 45);

            FillRect(
                image,
                0, 0,
                width, 85,
                bannerColor);

            int shotCount =
                Math.Clamp(
                    player.Shots,
                    0,
                    ShotsPerPlayer);

            for (int i = 0;
                 i < ShotsPerPlayer;
                 i++)
            {
                int x =
                    390 + i * 55;

                if (i < shotCount)
                {
                    FillCircle(
                        image,
                        x, 42,
                        15,
                        new Rgba32(255, 255, 255));
                }
                else
                {
                    DrawCircle(
                        image,
                        x, 42,
                        15,
                        3,
                        new Rgba32(255, 255, 255));
                }
            }

            for (int i = 0;
                 i < Math.Min(
                     player.Goals,
                     ShotsPerPlayer);
                 i++)
            {
                int x =
                    390 + i * 55;

                FillCircle(
                    image,
                    x, 95,
                    9,
                    new Rgba32(255, 215, 0));
            }

            using var stream =
                new MemoryStream();

            image.Save(
                stream,
                new JpegEncoder
                {
                    Quality = 90
                });

            return stream.ToArray();
        }

        private static void DrawGoal(
            Image<Rgba32> image)
        {
            Rgba32 white =
                new Rgba32(245, 245, 245);

            DrawLine(image, 320, 110, 680, 110, 8, white);
            DrawLine(image, 320, 110, 320, 250, 8, white);
            DrawLine(image, 680, 110, 680, 250, 8, white);

            for (int x = 340; x < 680; x += 30)
            {
                DrawLine(
                    image,
                    x, 115,
                    x, 245,
                    1,
                    new Rgba32(220, 220, 220));
            }

            for (int y = 130; y < 250; y += 25)
            {
                DrawLine(
                    image,
                    325, y,
                    675, y,
                    1,
                    new Rgba32(220, 220, 220));
            }
        }

        private static void DrawKeeper(
            Image<Rgba32> image,
            int x,
            int y)
        {
            Rgba32 skin =
                new Rgba32(230, 175, 135);

            Rgba32 shirt =
                new Rgba32(30, 80, 180);

            Rgba32 pants =
                new Rgba32(25, 25, 35);

            FillCircle(
                image,
                x, y - 55,
                25,
                skin);

            FillRect(
                image,
                x - 28,
                y - 30,
                56,
                80,
                shirt);

            DrawLine(
                image,
                x - 20, y - 15,
                x - 75, y - 45,
                15,
                shirt);

            DrawLine(
                image,
                x + 20, y - 15,
                x + 75, y - 45,
                15,
                shirt);

            FillCircle(
                image,
                x - 78, y - 47,
                12,
                new Rgba32(240, 210, 50));

            FillCircle(
                image,
                x + 78, y - 47,
                12,
                new Rgba32(240, 210, 50));

            DrawLine(
                image,
                x - 15, y + 45,
                x - 45, y + 100,
                18,
                pants);

            DrawLine(
                image,
                x + 15, y + 45,
                x + 45, y + 100,
                18,
                pants);
        }

        private static void DrawGoalEffect(
            Image<Rgba32> image,
            int x,
            int y)
        {
            Rgba32 yellow =
                new Rgba32(255, 220, 30);

            for (int i = 0; i < 12; i++)
            {
                double angle =
                    i * Math.PI * 2 / 12;

                int x2 =
                    x +
                    (int)(Math.Cos(angle) * 60);

                int y2 =
                    y +
                    (int)(Math.Sin(angle) * 60);

                DrawLine(
                    image,
                    x, y,
                    x2, y2,
                    5,
                    yellow);
            }
        }

        private static void DrawSaveEffect(
            Image<Rgba32> image,
            int x,
            int y)
        {
            Rgba32 red =
                new Rgba32(230, 50, 50);

            DrawLine(
                image,
                x - 55, y - 55,
                x + 55, y + 55,
                8,
                red);

            DrawLine(
                image,
                x + 55, y - 55,
                x - 55, y + 55,
                8,
                red);
        }

        private static void FillRect(
            Image<Rgba32> image,
            int x,
            int y,
            int width,
            int height,
            Rgba32 color)
        {
            int x1 = Math.Max(0, x);
            int y1 = Math.Max(0, y);

            int x2 =
                Math.Min(
                    image.Width,
                    x + width);

            int y2 =
                Math.Min(
                    image.Height,
                    y + height);

            if (x1 >= x2 || y1 >= y2)
                return;

            image.ProcessPixelRows(
                rows =>
                {
                    for (int yy = y1; yy < y2; yy++)
                    {
                        Span<Rgba32> row =
                            rows.GetRowSpan(yy);

                        for (int xx = x1; xx < x2; xx++)
                            row[xx] = color;
                    }
                });
        }

        private static void DrawRect(
            Image<Rgba32> image,
            int x,
            int y,
            int width,
            int height,
            int thickness,
            Rgba32 color)
        {
            FillRect(
                image,
                x, y,
                width, thickness,
                color);

            FillRect(
                image,
                x,
                y + height - thickness,
                width,
                thickness,
                color);

            FillRect(
                image,
                x, y,
                thickness,
                height,
                color);

            FillRect(
                image,
                x + width - thickness,
                y,
                thickness,
                height,
                color);
        }

        private static void DrawLine(
            Image<Rgba32> image,
            int x1,
            int y1,
            int x2,
            int y2,
            int thickness,
            Rgba32 color)
        {
            int dx = Math.Abs(x2 - x1);
            int dy = Math.Abs(y2 - y1);

            int sx = x1 < x2 ? 1 : -1;
            int sy = y1 < y2 ? 1 : -1;

            int err = dx - dy;

            int x = x1;
            int y = y1;

            while (true)
            {
                FillCircle(
                    image,
                    x,
                    y,
                    Math.Max(1, thickness / 2),
                    color);

                if (x == x2 && y == y2)
                    break;

                int e2 = 2 * err;

                if (e2 > -dy)
                {
                    err -= dy;
                    x += sx;
                }

                if (e2 < dx)
                {
                    err += dx;
                    y += sy;
                }
            }
        }

        private static void FillCircle(
            Image<Rgba32> image,
            int centerX,
            int centerY,
            int radius,
            Rgba32 color)
        {
            int radiusSquared =
                radius * radius;

            int minX =
                Math.Max(
                    0,
                    centerX - radius);

            int maxX =
                Math.Min(
                    image.Width - 1,
                    centerX + radius);

            int minY =
                Math.Max(
                    0,
                    centerY - radius);

            int maxY =
                Math.Min(
                    image.Height - 1,
                    centerY + radius);

            image.ProcessPixelRows(
                rows =>
                {
                    for (int y = minY; y <= maxY; y++)
                    {
                        int dy =
                            y - centerY;

                        Span<Rgba32> row =
                            rows.GetRowSpan(y);

                        for (int x = minX; x <= maxX; x++)
                        {
                            int dx =
                                x - centerX;

                            if (dx * dx +
                                dy * dy <=
                                radiusSquared)
                            {
                                row[x] = color;
                            }
                        }
                    }
                });
        }

        private static void DrawCircle(
            Image<Rgba32> image,
            int centerX,
            int centerY,
            int radius,
            int thickness,
            Rgba32 color)
        {
            int outer = radius;

            int inner =
                Math.Max(
                    0,
                    radius - thickness);

            int outerSquared =
                outer * outer;

            int innerSquared =
                inner * inner;

            int minX =
                Math.Max(
                    0,
                    centerX - outer);

            int maxX =
                Math.Min(
                    image.Width - 1,
                    centerX + outer);

            int minY =
                Math.Max(
                    0,
                    centerY - outer);

            int maxY =
                Math.Min(
                    image.Height - 1,
                    centerY + outer);

            image.ProcessPixelRows(
                rows =>
                {
                    for (int y = minY; y <= maxY; y++)
                    {
                        int dy =
                            y - centerY;

                        Span<Rgba32> row =
                            rows.GetRowSpan(y);

                        for (int x = minX; x <= maxX; x++)
                        {
                            int dx =
                                x - centerX;

                            int distance =
                                dx * dx +
                                dy * dy;

                            if (distance <= outerSquared &&
                                distance >= innerSquared)
                            {
                                row[x] = color;
                            }
                        }
                    }
                });
        }

        private static async Task SendMessage(
            string groupId,
            string message)
        {
            try
            {
                if (_client == null)
                    return;

                await _client.Message(
                    groupId,
                    message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "Send Message Error: " +
                    ex.Message);
            }
        }

        // ================================
        // إرسال الصور - الطريقة الصحيحة
        // ================================
        private static async Task SendImage(
            string groupId,
            byte[] imageBytes)
        {
            try
            {
                if (_client == null)
                {
                    Console.WriteLine(
                        "IMAGE ERROR: client is null");
                    return;
                }

                if (imageBytes == null ||
                    imageBytes.Length == 0)
                {
                    Console.WriteLine(
                        "IMAGE ERROR: image is empty");
                    return;
                }

                Console.WriteLine(
                    "================================");

                Console.WriteLine(
                    "IMAGE TEST");

                Console.WriteLine(
                    "Group: " + groupId);

                Console.WriteLine(
                    "Bytes: " + imageBytes.Length);

                // الطريقة الصحيحة لإرسال صورة في WolfLive.Api
                var result =
                    await _client.GroupMessage(
                        groupId,
                        imageBytes);

                Console.WriteLine(
                    "IMAGE SENT!");

                Console.WriteLine(
                    "Response: " + result);

                Console.WriteLine(
                    "================================");
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "IMAGE SEND ERROR:");

                Console.WriteLine(
                    ex.ToString());
            }
        }

        private static string GetMessageText(
            object message)
        {
            return GetStringProperty(
                message,
                "Text",
                "Message",
                "Content",
                "Body")
                ?? "";
        }

        private static string GetGroupId(
            object message)
        {
            string? value =
                GetStringProperty(
                    message,
                    "GroupId",
                    "RoomId",
                    "ChatId",
                    "ChannelId",
                    "ConversationId");

            return value ?? "";
        }

        private static string GetUserId(
            object message)
        {
            string? value =
                GetStringProperty(
                    message,
                    "UserId",
                    "SenderId",
                    "FromId",
                    "AuthorId");

            return value ?? "";
        }

        private static string GetUserName(
            object message)
        {
            string? value =
                GetStringProperty(
                    message,
                    "UserName",
                    "SenderName",
                    "FromName",
                    "AuthorName",
                    "Name");

            if (!string.IsNullOrWhiteSpace(value))
                return value;

            foreach (string propertyName in new[]
            {
                "Sender",
                "User",
                "Author",
                "From"
            })
            {
                try
                {
                    PropertyInfo? property =
                        message.GetType()
                            .GetProperty(
                                propertyName,
                                BindingFlags.Public |
                                BindingFlags.Instance |
                                BindingFlags.IgnoreCase);

                    object? obj =
                        property?.GetValue(message);

                    if (obj != null)
                    {
                        string? nested =
                            GetStringProperty(
                                obj,
                                "Name",
                                "UserName",
                                "DisplayName");

                        if (!string.IsNullOrWhiteSpace(nested))
                            return nested;
                    }
                }
                catch
                {
                }
            }

            return "لاعب";
        }

        private static string? GetStringProperty(
            object obj,
            params string[] propertyNames)
        {
            foreach (string name in propertyNames)
            {
                try
                {
                    PropertyInfo? property =
                        obj.GetType()
                            .GetProperty(
                                name,
                                BindingFlags.Public |
                                BindingFlags.Instance |
                                BindingFlags.IgnoreCase);

                    if (property == null)
                        continue;

                    object? value =
                        property.GetValue(obj);

                    if (value == null)
                        continue;

                    string result =
                        value.ToString() ?? "";

                    if (!string.IsNullOrWhiteSpace(result))
                        re
