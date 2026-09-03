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

        public List<PenaltyPlayer> Players { get; set; } =
            new List<PenaltyPlayer>();

        public int CurrentPlayerIndex { get; set; } = 0;

        public bool Started { get; set; } = false;

        public bool TurnAnswered { get; set; } = false;

        public int TurnId { get; set; } = 0;

        public CancellationTokenSource? TurnCancellation { get; set; }
    }

    public static class Program
    {
        private static IWolfClient? _client;

        private static readonly Dictionary<string, PenaltyGame> Games =
            new Dictionary<string, PenaltyGame>();

        private static readonly object GameLock = new object();

        private static readonly Random Random =
            new Random();

        private const int MaxPlayers = 10;
        private const int MinPlayers = 2;

        private const int ShotsPerPlayer = 5;

        // الوقت المطلوب
        private const int TurnSeconds = 25;

        public static async Task Main(string[] args)
        {
            Console.WriteLine("================================");
            Console.WriteLine("      PENALTY BOT");
            Console.WriteLine("================================");

            try
            {
                await ConnectBot();

                Console.WriteLine("BOT STARTED");

                await Task.Delay(Timeout.Infinite);
            }
            catch (Exception ex)
            {
                Console.WriteLine("MAIN ERROR:");
                Console.WriteLine(ex.ToString());
            }
        }

        private static async Task ConnectBot()
        {
            string email =
                Environment.GetEnvironmentVariable("WOLF_EMAIL")
                ?? "";

            string password =
                Environment.GetEnvironmentVariable("WOLF_PASSWORD")
                ?? "";

            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                Console.WriteLine(
                    "ERROR: WOLF_EMAIL or WOLF_PASSWORD is missing.");

                return;
            }

            Console.WriteLine("Creating Wolf client...");

            _client = new WolfClient();

            RegisterMessageEvents();

            Console.WriteLine("Logging in...");

            bool result =
                await _client.Login(email, password);

            Console.WriteLine(
                "Login: " +
                (result ? "SUCCESS" : "FAILED"));
        }

        // ============================================================
        // MESSAGE EVENT
        // ============================================================

        private static void RegisterMessageEvents()
        {
            if (_client == null)
                return;

            try
            {
                var events =
                    _client.GetType()
                        .GetEvents(
                            BindingFlags.Instance |
                            BindingFlags.Public |
                            BindingFlags.NonPublic);

                foreach (var ev in events)
                {
                    if (!ev.Name.Contains(
                            "message",
                            StringComparison.OrdinalIgnoreCase))
                        continue;

                    Console.WriteLine(
                        "Found message event: " +
                        ev.Name);

                    try
                    {
                        AttachDynamicEvent(ev);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            "Event attach failed: " +
                            ev.Name);

                        Console.WriteLine(
                            ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "REGISTER EVENT ERROR:");

                Console.WriteLine(
                    ex.ToString());
            }
        }

        private static void AttachDynamicEvent(EventInfo ev)
        {
            if (_client == null)
                return;

            if (ev.EventHandlerType == null)
                return;

            var invoke =
                ev.EventHandlerType.GetMethod("Invoke");

            if (invoke == null)
                return;

            var parameters =
                invoke.GetParameters();

            var parameterTypes =
                parameters
                    .Select(p => p.ParameterType)
                    .ToArray();

            var method =
                typeof(Program).GetMethod(
                    nameof(DynamicMessageEvent),
                    BindingFlags.Static |
                    BindingFlags.NonPublic);

            if (method == null)
                return;

            // إنشاء Delegate ديناميكي
            var dynamicMethod =
                new DynamicEventDelegate(
                    ev.EventHandlerType,
                    parameters.Length);

            ev.AddEventHandler(
                _client,
                dynamicMethod.CreateDelegate(
                    parameterTypes,
                    method));
        }

        private sealed class DynamicEventDelegate
        {
            private readonly Type _delegateType;
            private readonly int _parameterCount;

            public DynamicEventDelegate(
                Type delegateType,
                int parameterCount)
            {
                _delegateType = delegateType;
                _parameterCount = parameterCount;
            }

            public Delegate CreateDelegate(
                Type[] parameterTypes,
                MethodInfo target)
            {
                /*
                 * نحاول ربط الحدث بالدالة العامة.
                 * إذا كانت مكتبة Wolf تغيّر شكل الحدث،
                 * نستخدم الحدث الموجود ونعالج الرسالة بالانعكاس.
                 */

                var invoke =
                    _delegateType.GetMethod("Invoke");

                if (invoke == null)
                    throw new Exception(
                        "Invalid event delegate.");

                var parameters =
                    invoke.GetParameters();

                var dynamicAssembly =
                    System.Linq.Expressions.Expression
                        .GetActionType(
                            parameters
                                .Select(x => x.ParameterType)
                                .ToArray());

                var expressions =
                    parameters
                        .Select(
                            p =>
                                System.Linq.Expressions.Expression
                                    .Convert(
                                        System.Linq.Expressions.Expression
                                            .Parameter(
                                                p.ParameterType,
                                                p.Name ?? "p"),
                                        typeof(object)))
                        .ToArray();

                var array =
                    System.Linq.Expressions.Expression
                        .NewArrayInit(
                            typeof(object),
                            expressions);

                var call =
                    System.Linq.Expressions.Expression
                        .Call(
                            target,
                            array);

                var lambda =
                    System.Linq.Expressions.Expression
                        .Lambda(
                            _delegateType,
                            call,
                            parameters
                                .Select(
                                    p =>
                                        System.Linq.Expressions.Expression
                                            .Parameter(
                                                p.ParameterType,
                                                p.Name ?? "p"))
                                .ToArray());

                return lambda.Compile();
            }
        }

        private static void DynamicMessageEvent(params object[] args)
        {
            _ = Task.Run(
                async () =>
                {
                    try
                    {
                        await HandleMessageObject(args);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            "MESSAGE HANDLER ERROR:");

                        Console.WriteLine(
                            ex.ToString());
                    }
                });
        }

        private static async Task HandleMessageObject(
            object[] args)
        {
            if (args == null ||
                args.Length == 0)
                return;

            object? messageObject = null;

            foreach (var arg in args)
            {
                if (arg == null)
                    continue;

                var text =
                    GetMessageText(arg);

                if (!string.IsNullOrWhiteSpace(text))
                {
                    messageObject = arg;
                    break;
                }
            }

            if (messageObject == null)
            {
                messageObject = args[0];
            }

            string textMessage =
                GetMessageText(messageObject);

            if (string.IsNullOrWhiteSpace(textMessage))
                return;

            string groupId =
                GetGroupId(messageObject);

            string userId =
                GetUserId(messageObject);

            string userName =
                GetUserName(messageObject);

            if (string.IsNullOrWhiteSpace(groupId))
            {
                // نحاول استخراج GroupId من جميع arguments
                foreach (var arg in args)
                {
                    if (arg == null)
                        continue;

                    string possible =
                        GetGroupId(arg);

                    if (!string.IsNullOrWhiteSpace(possible))
                    {
                        groupId = possible;
                        break;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(userId))
            {
                foreach (var arg in args)
                {
                    if (arg == null)
                        continue;

                    string possible =
                        GetUserId(arg);

                    if (!string.IsNullOrWhiteSpace(possible))
                    {
                        userId = possible;
                        break;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(userName))
            {
                foreach (var arg in args)
                {
                    if (arg == null)
                        continue;

                    string possible =
                        GetUserName(arg);

                    if (!string.IsNullOrWhiteSpace(possible))
                    {
                        userName = possible;
                        break;
                    }
                }
            }

            Console.WriteLine(
                $"MESSAGE | Group={groupId} | User={userName} | Text={textMessage}");

            if (string.IsNullOrWhiteSpace(groupId))
            {
                Console.WriteLine(
                    "Message has no group ID.");

                return;
            }

            await ProcessCommand(
                groupId,
                userId,
                userName,
                textMessage);
        }

        // ============================================================
        // COMMANDS
        // ============================================================

        private static async Task ProcessCommand(
            string groupId,
            string userId,
            string userName,
            string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            string text =
                message.Trim();

            // مساعدة
            if (text.Equals(
                    "!جزاء",
                    StringComparison.OrdinalIgnoreCase) ||
                text.Equals(
                    "!جزاء مساعدة",
                    StringComparison.OrdinalIgnoreCase))
            {
                await SendMessage(
                    groupId,
                    "⚽ لعبة الجزاء\n\n" +
                    "!جزاء انضم — الانضمام للعبة\n" +
                    "!جزاء لاعبين — عرض اللاعبين\n" +
                    "!جزاء بدء — بدء اللعبة\n" +
                    "!جزاء حالة — حالة اللعبة\n" +
                    "!جزاء انهاء — إنهاء اللعبة\n\n" +
                    "بعد بدء اللعبة عندك 25 ثانية للتسديد.\n" +
                    "إذا انتهى الوقت ينشال اللاعب من اللعبة فقط.");
                
                return;
            }

            // انضمام
            if (text.Equals(
                    "!جزاء انضم",
                    StringComparison.OrdinalIgnoreCase))
            {
                await JoinGame(
                    groupId,
                    userId,
                    userName);

                return;
            }

            // اللاعبين
            if (text.Equals(
                    "!جزاء لاعبين",
                    StringComparison.OrdinalIgnoreCase))
            {
                await ShowPlayers(groupId);

                return;
            }

            // بدء
            if (text.Equals(
                    "!جزاء بدء",
                    StringComparison.OrdinalIgnoreCase))
            {
                await StartGame(groupId);

                return;
            }

            // حالة
            if (text.Equals(
                    "!جزاء حالة",
                    StringComparison.OrdinalIgnoreCase))
            {
                await ShowStatus(groupId);

                return;
            }

            // إنهاء
            if (text.Equals(
                    "!جزاء انهاء",
                    StringComparison.OrdinalIgnoreCase))
            {
                await EndGame(groupId);

                return;
            }

            // التسديد
            if (text == "1" ||
                text == "2" ||
                text == "3")
            {
                int direction =
                    int.Parse(text);

                await ProcessShot(
                    groupId,
                    userId,
                    direction);

                return;
            }
        }

        // ============================================================
        // JOIN
        // ============================================================

        private static async Task JoinGame(
            string groupId,
            string userId,
            string userName)
        {
            if (string.IsNullOrWhiteSpace(userId))
                userId = userName;

            PenaltyGame game;

            lock (GameLock)
            {
                if (!Games.TryGetValue(
                        groupId,
                        out game!))
                {
                    game =
                        new PenaltyGame
                        {
                            GroupId = groupId
                        };

                    Games[groupId] = game;
                }

                if (game.Started)
                {
                    _ = SendMessage(
                        groupId,
                        "⚠️ اللعبة بدأت بالفعل.");

                    return;
                }

                if (game.Players.Any(
                        p => p.UserId == userId))
                {
                    _ = SendMessage(
                        groupId,
                        $"⚠️ {userName} أنت مسجل باللعبة بالفعل.");

                    return;
                }

                if (game.Players.Count >= MaxPlayers)
                {
                    _ = SendMessage(
                        groupId,
                        "❌ اللعبة ممتلئة. الحد الأقصى 10 لاعبين.");

                    return;
                }

                int number =
                    game.Players.Count + 1;

                game.Players.Add(
                    new PenaltyPlayer
                    {
                        UserId = userId,
                        Name = string.IsNullOrWhiteSpace(userName)
                            ? "لاعب"
                            : userName,
                        Number = number
                    });
            }

            await SendMessage(
                groupId,
                $"✅ {userName} انضم للعبة الجزاء.\n" +
                $"رقمك: {GetPlayerNumber(groupId, userId)}\n" +
                $"👥 عدد اللاعبين: {GetPlayerCount(groupId)}/{MaxPlayers}");
        }

        private static int GetPlayerNumber(
            string groupId,
            string userId)
        {
            lock (GameLock)
            {
                if (!Games.TryGetValue(
                        groupId,
                        out var game))
                    return 0;

                return game.Players
                    .FirstOrDefault(
                        p => p.UserId == userId)
                    ?.Number ?? 0;
            }
        }

        private static int GetPlayerCount(
            string groupId)
        {
            lock (GameLock)
            {
                if (!Games.TryGetValue(
                        groupId,
                        out var game))
                    return 0;

                return game.Players.Count;
            }
        }

        // ============================================================
        // SHOW PLAYERS
        // ============================================================

        private static async Task ShowPlayers(
            string groupId)
        {
            PenaltyGame? game;

            lock (GameLock)
            {
                Games.TryGetValue(
                    groupId,
                    out game);
            }

            if (game == null ||
                game.Players.Count == 0)
            {
                await SendMessage(
                    groupId,
                    "لا يوجد لاعبين حالياً.\n" +
                    "اكتب !جزاء انضم");

                return;
            }

            var lines =
                new List<string>();

            lines.Add(
                "⚽ لاعبين لعبة الجزاء:");

            foreach (var player in game.Players)
            {
                string status =
                    player.Eliminated
                        ? "❌ خرج"
                        : "✅";

                lines.Add(
                    $"{player.Number}. {player.Name} {status} " +
                    $"({player.Goals} هدف)");
            }

            lines.Add("");
            lines.Add(
                game.Started
                    ? "🔥 اللعبة بدأت"
                    : "⏳ بانتظار البدء");

            await SendMessage(
                groupId,
                string.Join(
                    "\n",
                    lines));
        }

        // ============================================================
        // START GAME
        // ============================================================

        private static async Task StartGame(
            string groupId)
        {
            PenaltyGame? game;

            lock (GameLock)
            {
                if (!Games.TryGetValue(
                        groupId,
                        out game))
                {
                    game = null;
                }
                else
                {
                    if (game.Started)
                    {
                        game = null;
                    }
                    else if (game.Players.Count < MinPlayers)
                    {
                        game = null;
                    }
                    else
                    {
                        game.Started = true;
                        game.CurrentPlayerIndex = 0;

                        foreach (var player in game.Players)
                        {
                            player.Shots = 0;
                            player.Goals = 0;
                            player.Eliminated = false;
                        }
                    }
                }
            }

            if (game == null)
            {
                PenaltyGame? existing;

                lock (GameLock)
                {
                    Games.TryGetValue(
                        groupId,
                        out existing);
                }

                if (existing == null)
                {
                    await SendMessage(
                        groupId,
                        "❌ ماكو لعبة حالياً.");
                }
                else if (existing.Started)
                {
                    await SendMessage(
                        groupId,
                        "⚠️ اللعبة بدأت بالفعل.");
                }
                else if (existing.Players.Count < MinPlayers)
                {
                    await SendMessage(
                        groupId,
                        $"❌ لازم {MinPlayers} لاعبين على الأقل حتى تبدأ.");
                }

                return;
            }

            await SendMessage(
                groupId,
                "⚽🔥 بدأت لعبة الجزاء!\n\n" +
                $"👥 اللاعبين: {game.Players.Count}\n" +
                $"🎯 كل لاعب عنده {ShotsPerPlayer} تسديدات.\n" +
                $"⏱️ عندك {TurnSeconds} ثانية لكل تسديدة.\n\n" +
                "اختار الاتجاه:\n" +
                "1️⃣ يسار\n" +
                "2️⃣ وسط\n" +
                "3️⃣ يمين");

            await Task.Delay(1000);

            await StartTurn(groupId);
        }

        // ============================================================
        // START TURN
        // ============================================================

        private static async Task StartTurn(
            string groupId)
        {
            PenaltyPlayer? player = null;
            int turnId = 0;

            lock (GameLock)
            {
                if (!Games.TryGetValue(
                        groupId,
                        out var game))
                    return;

                if (!game.Started)
                    return;

                // تخطي اللاعبين الخارجين
                while (
                    game.CurrentPlayerIndex <
                    game.Players.Count &&
                    (
                        game.Players[
                            game.CurrentPlayerIndex]
                            .Eliminated ||
                        game.Players[
                            game.CurrentPlayerIndex]
                            .Shots >= ShotsPerPlayer
                    ))
                {
                    game.CurrentPlayerIndex++;
                }

                if (game.CurrentPlayerIndex >=
                    game.Players.Count)
                {
                    // لا نستدعي FinishGame داخل lock
                    player = null;
                }
                else
                {
                    player =
                        game.Players[
                            game.CurrentPlayerIndex];

                    game.TurnAnswered = false;

                    game.TurnId++;

                    turnId =
                        game.TurnId;

                    try
                    {
                        game.TurnCancellation?
                            .Cancel();
                    }
                    catch
                    {
                    }

                    game.TurnCancellation =
                        new CancellationTokenSource();
                }
            }

            if (player == null)
            {
                await FinishGame(groupId);
                return;
            }

            await SendMessage(
                groupId,
                $"🎯 دور اللاعب: {player.Name}\n" +
                $"رقم اللاعب: {player.Number}\n" +
                $"التسديدات: {player.Shots}/{ShotsPerPlayer}\n" +
                $"الأهداف: {player.Goals}\n\n" +
                "اختار بسرعة:\n" +
                "1️⃣ يسار\n" +
                "2️⃣ وسط\n" +
                "3️⃣ يمين\n\n" +
                $"⏱️ عندك {TurnSeconds} ثانية فقط!");

            // إنشاء الصورة
            try
            {
                byte[] image =
                    CreatePenaltyImage(
                        player,
                        false,
                        0,
                        0);

                await SendImage(
                    groupId,
                    image);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "CREATE/SEND IMAGE ERROR:");

                Console.WriteLine(
                    ex.ToString());
            }

            _ = RunTurnTimeout(
                groupId,
                player.UserId,
                turnId);
        }

        // ============================================================
        // TIMEOUT
        // ============================================================

        private static async Task RunTurnTimeout(
            string groupId,
            string userId,
            int turnId)
        {
            CancellationToken token;

            lock (GameLock)
            {
                if (!Games.TryGetValue(
                        groupId,
                        out var game))
                    return;

                if (game.TurnCancellation == null)
                    return;

                token =
                    game.TurnCancellation.Token;
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(
                        TurnSeconds),
                    token);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            PenaltyPlayer? player = null;

            lock (GameLock)
            {
                if (!Games.TryGetValue(
                        groupId,
                        out var game))
                    return;

                if (game.TurnId != turnId)
                    return;

                if (game.TurnAnswered)
                    return;

                player =
                    game.Players
                        .FirstOrDefault(
                            p => p.UserId == userId);

                if (player == null)
                    return;

                if (player.Eliminated)
                    return;

                // اللاعب ينشال من اللعبة فقط
                player.Eliminated = true;

                game.TurnAnswered = true;

                game.CurrentPlayerIndex++;
            }

            await SendMessage(
                groupId,
                $"⏰ انتهى الوقت!\n\n" +
                $"❌ اللاعب {player.Name} خرج من لعبة الجزاء.\n" +
                $"⚠️ لم يتم طرده من الروم.\n\n" +
                "اللعبة تكمل مع باقي اللاعبين.");

            await Task.Delay(500);

            await CheckGameAfterTurn(
                groupId);
        }

        // ============================================================
        // PROCESS SHOT
        // ============================================================

        private static async Task ProcessShot(
            string groupId,
            string userId,
            int direction)
        {
            PenaltyPlayer? player = null;

            int turnId = 0;

            lock (GameLock)
            {
                if (!Games.TryGetValue(
                        groupId,
                        out var game))
                    return;

                if (!game.Started)
                    return;

                if (game.CurrentPlayerIndex < 0 ||
                    game.CurrentPlayerIndex >=
                    game.Players.Count)
                    return;

                player =
                    game.Players[
                        game.CurrentPlayerIndex];

                if (player.UserId != userId)
                    return;

                if (player.Eliminated)
                    return;

                if (game.TurnAnswered)
                    return;

                if (player.Shots >= ShotsPerPlayer)
                    return;

                game.TurnAnswered = true;

                turnId =
                    game.TurnId;

                try
                {
                    game.TurnCancellation?
                        .Cancel();
                }
                catch
                {
                }

                player.Shots++;
            }

            // حارس المرمى يختار اتجاه عشوائي
            int keeperDirection =
                Random.Next(1, 4);

            bool goal =
                keeperDirection != direction;

            lock (GameLock)
            {
                if (!Games.TryGetValue(
                        groupId,
                        out var game))
                    return;

                if (game.TurnId != turnId)
                    return;

                if (goal)
                {
                    player!.Goals++;
                }
            }

            string directionText =
                direction == 1
                    ? "يسار"
                    : direction == 2
                        ? "وسط"
                        : "يمين";

            string keeperText =
                keeperDirection == 1
                    ? "يسار"
                    : keeperDirection == 2
                        ? "وسط"
                        : "يمين";

            if (goal)
            {
                await SendMessage(
                    groupId,
                    $"⚽🔥 هــــــــدف!\n\n" +
                    $"👤 اللاعب: {player!.Name}\n" +
                    $"🎯 التسديدة: {directionText}\n" +
                    $"🧤 الحارس: {keeperText}\n\n" +
                    $"⚽ الأهداف: {player.Goals}\n" +
                    $"🎯 التسديدات: {player.Shots}/{ShotsPerPlayer}");
            }
            else
            {
                await SendMessage(
                    groupId,
                    $"🧤❌ تصــــــــدى الحارس!\n\n" +
                    $"👤 اللاعب: {player!.Name}\n" +
                    $"🎯 التسديدة: {directionText}\n" +
                    $"🧤 الحارس: {keeperText}\n\n" +
                    $"⚽ الأهداف: {player.Goals}\n" +
                    $"🎯 التسديدات: {player.Shots}/{ShotsPerPlayer}");
            }

            // صورة النتيجة
            try
            {
                byte[] image =
                    CreatePenaltyImage(
                        player!,
                        goal,
                        direction,
                        keeperDirection);

                await SendImage(
                    groupId,
                    image);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "RESULT IMAGE ERROR:");

                Console.WriteLine(
                    ex.ToString());
            }

            await Task.Delay(700);

            await CheckGameAfterTurn(
                groupId);
        }

        // ============================================================
        // CHECK GAME
        // ============================================================

        private static async Task CheckGameAfterTurn(
            string groupId)
        {
            bool finish = false;

            lock (GameLock)
            {
                if (!Games.TryGetValue(
                        groupId,
                        out var game))
                    return;

                int activePlayers =
                    game.Players.Count(
                        p =>
                            !p.Eliminated &&
                            p.Shots < ShotsPerPlayer);

                if (activePlayers == 0)
                {
                    finish = true;
                }
                else
                {
                    game.CurrentPlayerIndex++;
                }
            }

            if (finish)
            {
                await FinishGame(groupId);
                return;
            }

            await StartTurn(groupId);
        }

        // ============================================================
        // STATUS
        // ============================================================

        private static async Task ShowStatus(
            string groupId)
        {
            PenaltyGame? game;

            lock (GameLock)
            {
                Games.TryGetValue(
                    groupId,
                    out game);
            }

            if (game == null)
            {
                await SendMessage(
                    groupId,
                    "❌ لا توجد لعبة حالياً.");

                return;
            }

            var lines =
                new List<string>();

            lines.Add("⚽ حالة لعبة الجزاء");
            lines.Add("");

            lines.Add(
                game.Started
                    ? "🔥 الحالة: بدأت"
                    : "⏳ الحالة: انتظار");

            lines.Add(
                $"👥 اللاعبين: {game.Players.Count}");

            lines.Add("");

            foreach (var player in game.Players)
            {
                lines.Add(
                    $"{player.Number}. {player.Name} — " +
                    $"{player.Goals} أهداف / " +
                    $"{player.Shots} تسديدات" +
                    (player.Eliminated
                        ? " ❌ خرج"
                        : ""));
            }

            await SendMessage(
                groupId,
                string.Join(
                    "\n",
                    lines));
        }

        // ============================================================
        // END GAME
        // ============================================================

        private static async Task EndGame(
            string groupId)
        {
            bool exists;

            lock (GameLock)
            {
                exists =
                    Games.ContainsKey(groupId);

                if (exists)
                {
                    try
                    {
                        Games[groupId]
                            .TurnCancellation?
                            .Cancel();
                    }
                    catch
                    {
                    }

                    Games.Remove(groupId);
                }
            }

            await SendMessage(
                groupId,
                exists
                    ? "🛑 تم إنهاء لعبة الجزاء."
                    : "❌ لا توجد لعبة حالياً.");
        }

        // ============================================================
        // FINISH
        // ============================================================

        private static async Task FinishGame(
            string groupId)
        {
            List<PenaltyPlayer> players;

            lock (GameLock)
            {
                if (!Games.TryGetValue(
                        groupId,
                        out var game))
                    return;

                try
                {
                    game.TurnCancellation?
                        .Cancel();
                }
                catch
                {
                }

                players =
                    game.Players
                        .Where(
                            p => !p.Eliminated)
                        .OrderByDescending(
                            p => p.Goals)
                        .ThenByDescending(
                            p => p.Shots)
                        .ToList();
            }

            if (players.Count == 0)
            {
                await SendMessage(
                    groupId,
                    "🏁 انتهت اللعبة.\n" +
                    "❌ ماكو لاعب باقي.");

                lock (GameLock)
                {
                    Games.Remove(groupId);
                }

                return;
            }

            PenaltyPlayer winner =
                players[0];

            var lines =
                new List<string>();

            lines.Add(
                "🏆🏆 انتهت لعبة الجزاء! 🏆🏆");

            lines.Add("");

            lines.Add(
                $"🥇 الفائز: {winner.Name}");

            lines.Add(
                $"⚽ الأهداف: {winner.Goals}");

            lines.Add("");

            lines.Add(
                "📊 النتائج:");

            int rank = 1;

            foreach (var player in players)
            {
                lines.Add(
                    $"{rank}. {player.Name} — " +
                    $"{player.Goals} أهداف / " +
                    $"{player.Shots} تسديدات");

                rank++;
            }

            var eliminated =
                players.Count == 0
                    ? 0
                    : 0;

            lock (GameLock)
            {
                if (Games.TryGetValue(
                        groupId,
                        out var game))
                {
                    var outPlayers =
                        game.Players
                            .Where(
                                p => p.Eliminated)
                            .ToList();

                    if (outPlayers.Count > 0)
                    {
                        lines.Add("");
                        lines.Add(
                            "❌ اللاعبون الخارجون:");

                        foreach (
                            var p in outPlayers)
                        {
                            lines.Add(
                                $"• {p.Name}");
                        }
                    }

                    Games.Remove(groupId);
                }
            }

            await SendMessage(
                groupId,
                string.Join(
                    "\n",
                    lines));
        }

        // ============================================================
        // IMAGE
        // ============================================================

        private static byte[] CreatePenaltyImage(
            PenaltyPlayer player,
            bool goal,
            int playerDirection,
            int keeperDirection)
        {
            const int width = 900;
            const int height = 600;

            using var image =
                new Image<Rgba32>(
                    width,
                    height);

            // خلفية
            FillRect(
                image,
                0,
                0,
                width,
                height,
                new Rgba32(
                    15,
                    18,
                    25,
                    255));

            // الملعب
            FillRect(
                image,
                0,
                100,
                width,
                500,
                new Rgba32(
                    28,
                    120,
                    55,
                    255));

            // حدود منطقة الجزاء
            DrawRect(
                image,
                250,
                230,
                400,
                300,
                5,
                new Rgba32(
                    255,
                    255,
                    255,
                    255));

            // المرمى
            DrawRect(
                image,
                300,
                100,
                300,
                180,
                8,
                new Rgba32(
                    245,
                    245,
                    245,
                    255));

            // شبكة المرمى
            for (int x = 300; x <= 600; x += 30)
            {
                DrawLine(
                    image,
                    x,
                    100,
                    x,
                    280,
                    2,
                    new Rgba32(
                        220,
                        220,
                        220,
                        180));
            }

            for (int y = 100; y <= 280; y += 30)
            {
                DrawLine(
                    image,
                    300,
                    y,
                    600,
                    y,
                    2,
                    new Rgba32(
                        220,
                        220,
                        220,
                        180));
            }

            // الحارس
            DrawKeeper(
                image,
                keeperDirection);

            // الكرة
            int ballX = 450;
            int ballY = 490;

            if (playerDirection >= 1 &&
                playerDirection <= 3)
            {
                if (playerDirection == 1)
                    ballX = 390;
                else if (playerDirection == 2)
                    ballX = 450;
                else
                    ballX = 510;

                ballY = 330;
            }

            FillCircle(
                image,
                ballX,
                ballY,
                20,
                new Rgba32(
                    255,
                    255,
                    255,
                    255));

            DrawCircle(
                image,
                ballX,
                ballY,
                20,
                3,
                new Rgba32(
                    30,
                    30,
                    30,
                    255));

            // تأثير النتيجة
            if (playerDirection != 0)
            {
                if (goal)
                {
                    DrawGoalEffect(
                        image);
                }
                else
                {
                    DrawSaveEffect(
                        image);
                }
            }

            // إطار علوي
            DrawRect(
                image,
                20,
                20,
                width - 40,
                60,
                3,
                new Rgba32(
                    255,
                    215,
                    0,
                    255));

            using var ms =
                new MemoryStream();

            image.Save(
                ms,
                new JpegEncoder
                {
                    Quality = 90
                });

            return ms.ToArray();
        }

        // ============================================================
        // KEEPER
        // ============================================================

        private static void DrawKeeper(
            Image<Rgba32> image,
            int direction)
        {
            int x;

            if (direction == 1)
                x = 360;
            else if (direction == 2)
                x = 450;
            else
                x = 540;

            int y = 215;

            // الرأس
            FillCircle(
                image,
                x,
                y - 65,
                28,
                new Rgba32(
                    255,
                    210,
                    170,
                    255));

            // الجسم
            FillRect(
                image,
                x - 25,
                y - 35,
                50,
                80,
                new Rgba32(
                    30,
                    60,
                    220,
                    255));

            // الأرجل
            DrawLine(
                image,
                x,
                y + 45,
                x - 25,
                y + 95,
                15,
                new Rgba32(
                    20,
                    20,
                    20,
                    255));

            DrawLine(
                image,
                x,
                y + 45,
                x + 25,
                y + 95,
                15,
                new Rgba32(
                    20,
                    20,
                    20,
                    255));

            // الأيدي حسب اتجاه الحارس
            if (direction == 1)
            {
                DrawLine(
                    image,
                    x,
                    y - 20,
                    x - 100,
                    y - 60,
                    15,
                    new Rgba32(
                        30,
                        60,
                        220,
                        255));

                DrawLine(
                    image,
                    x,
                    y - 20,
                    x + 50,
                    y + 10,
                    15,
                    new Rgba32(
                        30,
                        60,
                        220,
                        255));
            }
            else if (direction == 2)
            {
                DrawLine(
                    image,
                    x,
                    y - 20,
                    x - 60,
                    y - 70,
                    15,
                    new Rgba32(
                        30,
                        60,
                        220,
                        255));

                DrawLine(
                    image,
                    x,
                    y - 20,
                    x + 60,
                    y - 70,
                    15,
                    new Rgba32(
                        30,
                        60,
                        220,
                        255));
            }
            else
            {
                DrawLine(
                    image,
                    x,
                    y - 20,
                    x + 100,
                    y - 60,
                    15,
                    new Rgba32(
                        30,
                        60,
                        220,
                        255));

                DrawLine(
                    image,
                    x,
                    y - 20,
                    x - 50,
                    y + 10,
                    15,
                    new Rgba32(
                        30,
                        60,
                        220,
                        255));
            }
        }

        // ============================================================
        // GOAL EFFECT
        // ============================================================

        private static void DrawGoalEffect(
            Image<Rgba32> image)
        {
            DrawCircle(
                image,
                450,
                190,
                100,
                6,
                new Rgba32(
                    255,
                    215,
                    0,
                    255));

            DrawCircle(
                image,
                450,
                190,
                75,
                4,
                new Rgba32(
                    255,
                    255,
                    255,
                    255));

            for (int i = 0; i < 16; i++)
            {
                double angle =
                    i * Math.PI * 2 / 16;

                int x1 =
                    450 +
                    (int)(100 * Math.Cos(angle));

                int y1 =
                    190 +
                    (int)(100 * Math.Sin(angle));

                int x2 =
                    450 +
                    (int)(135 * Math.Cos(angle));

                int y2 =
                    190 +
                    (int)(135 * Math.Sin(angle));

                DrawLine(
                    image,
                    x1,
                    y1,
                    x2,
                    y2,
                    5,
                    new Rgba32(
                        255,
                        215,
                        0,
                        255));
            }
        }

        // ============================================================
        // SAVE EFFECT
        // ============================================================

        private static void DrawSaveEffect(
            Image<Rgba32> image)
        {
            DrawCircle(
                image,
                450,
                190,
                90,
                6,
                new Rgba32(
                    255,
                    80,
                    80,
                    255));

            DrawCircle(
                image,
                450,
                190,
                65,
                4,
                new Rgba32(
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
                    "Group: " +
                    groupId);

                Console.WriteLine(
                    "Bytes: " +
                    imageBytes.Length);

                var result =
                    await _client.GroupMessage(
                        groupId,
                        imageBytes);

                Console.WriteLine(
                    "IMAGE SENT!");

                Console.WriteLine(
                    "Response: " +
                    result);

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
            object obj)
        {
            if (obj == null)
                return "";

            string[] names =
            {
                "Text",
                "Message",
                "Content",
                "Body",
                "MessageText"
            };

            foreach (string name in names)
            {
                string value =
                    GetStringProperty(
                        obj,
                        name);

                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return "";
        }

        private static string GetGroupId(
            object obj)
        {
            if (obj == null)
                return "";

            string[] names =
            {
                "GroupId",
                "GroupID",
                "RoomId",
                "RoomID",
                "ChatId",
                "ChatID",
                "ConversationId",
                "ConversationID"
            };

            foreach (string name in names)
            {
                string value =
                    GetStringProperty(
                        obj,
                        name);

                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return "";
        }

        private static string GetUserId(
            object obj)
        {
            if (obj == null)
                return "";

            string[] names =
            {
                "UserId",
                "UserID",
                "SenderId",
                "SenderID",
                "FromId",
                "FromID",
                "AuthorId",
                "AuthorID"
            };

            foreach (string name in names)
            {
                string value =
                    GetStringProperty(
                        obj,
                        name);

                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return "";
        }

        private static string GetUserName(
            object obj)
        {
            if (obj == null)
                return "";

            string[] names =
            {
                "UserName",
                "Username",
                "Name",
                "SenderName",
                "DisplayName",
                "NickName",
                "Nickname"
            };

            foreach (string name in names)
            {
                string value =
                    GetStringProperty(
                        obj,
                        name);

                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return "";
        }

        private static object? GetObjectProperty(
            object obj,
            string propertyName)
        {
            try
            {
                var property =
                    obj.GetType().GetProperty(
                        propertyName,
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.IgnoreCase);

                return property?.GetValue(obj);
            }
            catch
            {
                return null;
            }
        }

        private static string GetStringProperty(
            object obj,
            string propertyName)
        {
            try
            {
                var property =
                    obj.GetType().GetProperty(
                        propertyName,
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.IgnoreCase);

                if (property == null)
                    return "";

                object? value =
                    property.GetValue(obj);

                if (value == null)
                    return "";

                return value.ToString() ?? "";
            }
            catch
            {
                return "";
            }
        }
    }
                    }
