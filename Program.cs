using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WolfLive.Api;
using WolfLive.Api.Models;

namespace PenaltyBot
{
    public class PenaltyPlayer
    {
        public string UserId { get; set; } = "";
        public int Number { get; set; }
        public int Goals { get; set; } = 0;
        public int Shots { get; set; } = 0;
    }

    public class PenaltyGame
    {
        public string GroupId { get; set; } = "";

        public List<PenaltyPlayer> Players { get; set; } =
            new List<PenaltyPlayer>();

        public bool Started { get; set; } = false;

        public int CurrentPlayerIndex { get; set; } = 0;

        public bool WaitingForShot { get; set; } = false;

        public CancellationTokenSource? TurnTimeout { get; set; }

        public int TurnVersion { get; set; } = 0;

        public PenaltyPlayer? CurrentPlayer
        {
            get
            {
                if (Players.Count == 0)
                    return null;

                if (CurrentPlayerIndex < 0 ||
                    CurrentPlayerIndex >= Players.Count)
                    return null;

                return Players[CurrentPlayerIndex];
            }
        }
    }

    public class Program
    {
        private static IWolfClient Client = null!;

        // =========================================================
        // ألعاب مستقلة لكل روم
        // =========================================================

        private static readonly Dictionary<string, PenaltyGame> Games =
            new Dictionary<string, PenaltyGame>();

        private static readonly SemaphoreSlim GamesLock =
            new SemaphoreSlim(1, 1);

        private static readonly Random Random =
            new Random();

        private const int MaxPlayers = 10;
        private const int ShotsPerPlayer = 5;
        private const int TurnSeconds = 10;

        // =========================================================
        // MAIN
        // =========================================================

        public static async Task Main(string[] args)
        {
            string email =
                Environment.GetEnvironmentVariable("WOLF_EMAIL") ?? "";

            string password =
                Environment.GetEnvironmentVariable("WOLF_PASSWORD") ?? "";

            Console.WriteLine("=================================");
            Console.WriteLine("       PENALTY BOT STARTING");
            Console.WriteLine("=================================");

            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                Console.WriteLine(
                    "ERROR: WOLF_EMAIL or WOLF_PASSWORD missing."
                );

                return;
            }

            Client = new WolfClient();

            Client.On<WolfMessage>(
                "message send",
                OnWolfMessage
            );

            bool loggedIn =
                await LoginManually(email, password);

            if (!loggedIn)
            {
                Console.WriteLine("LOGIN FAILED.");
                return;
            }

            Console.WriteLine("LOGIN SUCCESS");

            // =====================================================
            // مهم:
            // لم نعد نستخدم روم ثابت.
            //
            // إذا كانت WolfLive.Api ترسل message send لكل الرومات
            // التي البوت موجود بها، فالبوت سيعمل عليها كلها.
            // =====================================================

            Console.WriteLine("=================================");
            Console.WriteLine("BOT IS ONLINE");
            Console.WriteLine("MULTI ROOM MODE ENABLED");
            Console.WriteLine("WAITING FOR MESSAGES...");
            Console.WriteLine("=================================");

            await Task.Delay(Timeout.Infinite);
        }

        // =========================================================
        // LOGIN
        // =========================================================

        private static async Task<bool> LoginManually(
            string email,
            string password)
        {
            try
            {
                Console.WriteLine("Connecting to WOLF...");

                var welcomeSource =
                    new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously
                    );

                Client.On<object>(
                    "welcome",
                    _ =>
                    {
                        Console.WriteLine("WELCOME RECEIVED");

                        welcomeSource.TrySetResult(true);
                    }
                );

                await Client.Connect();

                Console.WriteLine(
                    "Connected to WOLF server."
                );

                Console.WriteLine(
                    "Waiting for WOLF welcome..."
                );

                var completed =
                    await Task.WhenAny(
                        welcomeSource.Task,
                        Task.Delay(TimeSpan.FromSeconds(15))
                    );

                if (completed != welcomeSource.Task)
                {
                    Console.WriteLine(
                        "ERROR: WOLF welcome timeout."
                    );

                    return false;
                }

                Console.WriteLine("Sending login...");

                var user =
                    await Client.Emit<User>(
                        new Packet(
                            "security login",
                            new
                            {
                                username = email,
                                password = password
                            }
                        )
                    );

                if (user == null)
                {
                    Console.WriteLine(
                        "LOGIN RESPONSE IS NULL."
                    );

                    return false;
                }

                Client.Profiling.Profile = user;

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("LOGIN ERROR:");
                Console.WriteLine(ex);

                return false;
            }
        }

        // =========================================================
        // MESSAGE RECEIVED
        // =========================================================

        private static async void OnWolfMessage(
            WolfMessage wolfMessage)
        {
            try
            {
                if (wolfMessage == null)
                    return;

                var message =
                    new Message(wolfMessage);

                Console.WriteLine(
                    $"MESSAGE RECEIVED | " +
                    $"Group={message.GroupId} | " +
                    $"Content={message.Content}"
                );

                if (!message.IsGroup)
                    return;

                // =================================================
                // لم نعد نفحص GroupId ثابت
                // =================================================

                if (string.IsNullOrWhiteSpace(message.GroupId))
                    return;

                string content =
                    (message.Content ?? "").Trim();

                if (string.IsNullOrWhiteSpace(content))
                    return;

                await HandleMessage(
                    message,
                    content
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "MESSAGE ERROR:"
                );

                Console.WriteLine(ex);
            }
        }

        // =========================================================
        // HANDLE MESSAGE
        // =========================================================

        private static async Task HandleMessage(
            Message message,
            string content)
        {
            await GamesLock.WaitAsync();

            try
            {
                string lower =
                    content.ToLowerInvariant();

                string groupId =
                    message.GroupId ?? "";

                if (string.IsNullOrWhiteSpace(groupId))
                    return;

                // =================================================
                // HELP
                // =================================================

                if (lower == "!جزاء مساعدة" ||
                    lower == "!جزاء help")
                {
                    await Send(
                        message,
                        "⚽ لعبة ضربة الجزاء ⚽\n\n" +
                        "🎮 الأوامر:\n" +
                        "!جزاء — إنشاء لعبة\n" +
                        "!جزاء انضم — الانضمام\n" +
                        "!جزاء لاعبين — عرض اللاعبين\n" +
                        "!جزاء بدء — بدء اللعبة\n" +
                        "!جزاء حالة — حالة اللعبة\n" +
                        "!جزاء انهاء — إنهاء اللعبة\n\n" +
                        "🎯 أثناء دورك:\n" +
                        "1️⃣ يمين\n" +
                        "2️⃣ وسط\n" +
                        "3️⃣ يسار\n\n" +
                        "🥅 الحارس يختار مكاناً عشوائياً.\n" +
                        "إذا اختار نفس المكان تُصد التسديدة.\n" +
                        "إذا اختار مكاناً مختلفاً = هدف! ⚽\n\n" +
                        "⏱️ لديك 10 ثواني لكل تسديدة.\n" +
                        "🎯 لكل لاعب 5 تسديدات."
                    );

                    return;
                }

                // =================================================
                // CREATE GAME
                // =================================================

                if (lower == "!جزاء")
                {
                    await NewGame(message);
                    return;
                }

                // =================================================
                // GET GAME FOR THIS ROOM
                // =================================================

                PenaltyGame? game = GetGame(groupId);

                // =================================================
                // NO GAME
                // =================================================

                if (game == null)
                {
                    if (lower.StartsWith("!جزاء"))
                    {
                        await Send(
                            message,
                            "❌ لا توجد لعبة حالياً بهذا الروم.\n" +
                            "اكتب !جزاء لإنشاء لعبة جديدة."
                        );
                    }

                    return;
                }

                // =================================================
                // JOIN
                // =================================================

                if (lower == "!جزاء انضم" ||
                    lower == "!جزاء انضمام")
                {
                    await JoinGame(message, game);
                    return;
                }

                // =================================================
                // PLAYERS
                // =================================================

                if (lower == "!جزاء لاعبين")
                {
                    await ShowPlayers(message, game);
                    return;
                }

                // =================================================
                // START
                // =================================================

                if (lower == "!جزاء بدء")
                {
                    await StartGame(message, game);
                    return;
                }

                // =================================================
                // STATUS
                // =================================================

                if (lower == "!جزاء حالة")
                {
                    await ShowStatus(message, game);
                    return;
                }

                // =================================================
                // END
                // =================================================

                if (lower == "!جزاء انهاء" ||
                    lower == "!جزاء إنهاء")
                {
                    await EndGame(message, game);
                    return;
                }

                // =================================================
                // GAMEPLAY
                // =================================================

                if (game.Started &&
                    game.WaitingForShot)
                {
                    if (TryParseChoice(
                        content,
                        out int choice))
                    {
                        await ProcessShot(
                            message,
                            game,
                            choice
                        );

                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "HANDLE ERROR:"
                );

                Console.WriteLine(ex);
            }
            finally
            {
                GamesLock.Release();
            }
        }

        // =========================================================
        // GET GAME
        // =========================================================

        private static PenaltyGame? GetGame(
            string groupId)
        {
            if (Games.TryGetValue(
                groupId,
                out PenaltyGame? game))
            {
                return game;
            }

            return null;
        }

        // =========================================================
        // NEW GAME
        // =========================================================

        private static async Task NewGame(
            Message message)
        {
            string groupId =
                message.GroupId ?? "";

            if (string.IsNullOrWhiteSpace(groupId))
                return;

            if (Games.ContainsKey(groupId))
            {
                await Send(
                    message,
                    "⚠️ توجد لعبة بالفعل بهذا الروم.\n" +
                    "اكتب !جزاء لاعبين لمعرفة اللاعبين."
                );

                return;
            }

            var game =
                new PenaltyGame
                {
                    GroupId = groupId,
                    Started = false,
                    CurrentPlayerIndex = 0,
                    WaitingForShot = false
                };

            Games[groupId] = game;

            await Send(
                message,
                "⚽🔥 تم إنشاء لعبة ضربة الجزاء!\n\n" +
                "للانضمام اكتب:\n" +
                "!جزاء انضم\n\n" +
                "👥 الحد الأقصى: 10 لاعبين\n" +
                "🎯 كل لاعب لديه 5 تسديدات\n" +
                "⏱️ 10 ثواني لكل تسديدة\n\n" +
                "عند جاهزية اللاعبين اكتب:\n" +
                "!جزاء بدء"
            );
        }

        // =========================================================
        // JOIN
        // =========================================================

        private static async Task JoinGame(
            Message message,
            PenaltyGame game)
        {
            if (game.Started)
            {
                await Send(
                    message,
                    "❌ اللعبة بدأت بالفعل، لا يمكن الانضمام."
                );

                return;
            }

            string userId =
                message.UserId ?? "";

            if (string.IsNullOrWhiteSpace(userId))
            {
                await Send(
                    message,
                    "❌ تعذر معرفة حساب اللاعب."
                );

                return;
            }

            if (game.Players.Any(
                p => p.UserId == userId))
            {
                var existing =
                    game.Players.First(
                        p => p.UserId == userId
                    );

                await Send(
                    message,
                    $"⚠️ أنت مسجل بالفعل كـ اللاعب {existing.Number}️⃣."
                );

                return;
            }

            if (game.Players.Count >= MaxPlayers)
            {
                await Send(
                    message,
                    "❌ اللعبة مكتملة، الحد الأقصى 10 لاعبين."
                );

                return;
            }

            int number =
                game.Players.Count + 1;

            var player =
                new PenaltyPlayer
                {
                    UserId = userId,
                    Number = number
                };

            game.Players.Add(player);

            await Send(
                message,
                $"✅ تم انضمامك إلى اللعبة!\n\n" +
                $"👤 رقمك: اللاعب {number}️⃣\n" +
                $"👥 عدد اللاعبين: {game.Players.Count}/{MaxPlayers}\n\n" +
                "عند جاهزية اللاعبين اكتب:\n" +
                "!جزاء بدء"
            );
        }

        // =========================================================
        // SHOW PLAYERS
        // =========================================================

        private static async Task ShowPlayers(
            Message message,
            PenaltyGame game)
        {
            if (game.Players.Count == 0)
            {
                await Send(
                    message,
                    "👥 لا يوجد لاعبون حالياً."
                );

                return;
            }

            string text =
                "⚽ لاعبو لعبة ضربة الجزاء ⚽\n\n";

            foreach (var player in game.Players)
            {
                text +=
                    $"👤 اللاعب {player.Number}️⃣\n" +
                    $"⚽ الأهداف: {player.Goals}\n" +
                    $"🎯 التسديدات: {player.Shots}/{ShotsPerPlayer}\n\n";
            }

            await Send(
                message,
                text
            );
        }

        // =========================================================
        // START GAME
        // =========================================================

        private static async Task StartGame(
            Message message,
            PenaltyGame game)
        {
            if (game.Started)
            {
                await Send(
                    message,
                    "⚠️ اللعبة بدأت بالفعل."
                );

                return;
            }

            if (game.Players.Count < 2)
            {
                await Send(
                    message,
                    "❌ يجب أن يكون هناك لاعبان على الأقل لبدء اللعبة."
                );

                return;
            }

            game.Started = true;

            game.CurrentPlayerIndex = 0;

            game.WaitingForShot = true;

            game.TurnVersion++;

            await Send(
                message,
                "⚽🔥 بدأت لعبة ضربة الجزاء!\n\n" +
                $"👥 عدد اللاعبين: {game.Players.Count}\n" +
                $"🎯 كل لاعب لديه {ShotsPerPlayer} تسديدات\n" +
                $"⏱️ الوقت: {TurnSeconds} ثواني\n\n" +
                "🏆 اللاعب صاحب أكبر عدد من الأهداف يفوز!"
            );

            await Task.Delay(500);

            await SendCurrentTurn(game);

            StartShotTimeout(game);
        }

        // =========================================================
        // SEND CURRENT TURN
        // =========================================================

        private static async Task SendCurrentTurn(
            PenaltyGame game)
        {
            if (!game.Started ||
                !game.WaitingForShot)
                return;

            var player =
                game.CurrentPlayer;

            if (player == null)
                return;

            int shotNumber =
                player.Shots + 1;

            await SendToGroup(
                game.GroupId,
                "⚽ دور اللاعب " +
                $"{player.Number}️⃣!\n\n" +

                $"🎯 التسديدة رقم {shotNumber}/{ShotsPerPlayer}\n\n" +

                "اختار مكان التسديدة:\n" +
                "1️⃣ يمين\n" +
                "2️⃣ وسط\n" +
                "3️⃣ يسار\n\n" +

                $"⏱️ عندك {TurnSeconds} ثواني!"
            );
        }

        // =========================================================
        // PROCESS SHOT
        // =========================================================

        private static async Task ProcessShot(
            Message message,
            PenaltyGame game,
            int choice)
        {
            if (!game.Started ||
                !game.WaitingForShot)
                return;

            var player =
                game.CurrentPlayer;

            if (player == null)
                return;

            if (player.UserId != message.UserId)
            {
                await Send(
                    message,
                    $"⛔ مو دورك!\n" +
                    $"الآن دور اللاعب {player.Number}️⃣."
                );

                return;
            }

            if (choice < 1 ||
                choice > 3)
            {
                await Send(
                    message,
                    "❌ اختيار غير صحيح.\n" +
                    "اكتب 1 أو 2 أو 3."
                );

                return;
            }

            CancelTurnTimeout(game);

            game.WaitingForShot = false;

            game.TurnVersion++;

            player.Shots++;

            int goalkeeper =
                Random.Next(1, 4);

            string shotName =
                GetDirectionName(choice);

            string goalkeeperName =
                GetDirectionName(goalkeeper);

            bool goal =
                choice != goalkeeper;

            if (goal)
            {
                player.Goals++;

                await SendToGroup(
                    game.GroupId,
                    $"⚽🔥 هــــــــدف!\n\n" +
                    $"👤 اللاعب {player.Number}️⃣\n" +
                    $"🎯 التسديدة: {shotName}\n" +
                    $"🧤 الحارس: {goalkeeperName}\n\n" +
                    $"🥅 هدف رقم {player.Goals}!"
                );
            }
            else
            {
                await SendToGroup(
                    game.GroupId,
                    $"🧤❌ تــــــم التصدي!\n\n" +
                    $"👤 اللاعب {player.Number}️⃣\n" +
                    $"🎯 التسديدة: {shotName}\n" +
                    $"🧤 الحارس اختار: {goalkeeperName}\n\n" +
                    "الحارس قرأ التسديدة! 🔥"
                );
            }

            if (AllShotsFinished(game))
            {
                await Task.Delay(700);

                await FinishGame(game);

                return;
            }

            MoveToNextPlayer(game);

            await Task.Delay(500);

            await SendCurrentTurn(game);

            StartShotTimeout(game);
        }

        // =========================================================
        // MOVE NEXT PLAYER
        // =========================================================

        private static void MoveToNextPlayer(
            PenaltyGame game)
        {
            if (game.Players.Count == 0)
                return;

            int count =
                game.Players.Count;

            for (int i = 1; i <= count; i++)
            {
                int index =
                    (game.CurrentPlayerIndex + i)
                    % count;

                var player =
                    game.Players[index];

                if (player.Shots < ShotsPerPlayer)
                {
                    game.CurrentPlayerIndex =
                        index;

                    game.WaitingForShot = true;

                    game.TurnVersion++;

                    return;
                }
            }
        }

        // =========================================================
        // ALL SHOTS FINISHED
        // =========================================================

        private static bool AllShotsFinished(
            PenaltyGame game)
        {
            return game.Players.All(
                p => p.Shots >= ShotsPerPlayer
            );
        }

        // =========================================================
        // TIMEOUT
        // =========================================================

        private static void StartShotTimeout(
            PenaltyGame game)
        {
            CancelTurnTimeout(game);

            game.TurnTimeout =
                new CancellationTokenSource();

            CancellationToken token =
                game.TurnTimeout.Token;

            string groupId =
                game.GroupId;

            int version =
                game.TurnVersion;

            int playerIndex =
                game.CurrentPlayerIndex;

            _ = Task.Run(
                async () =>
                {
                    try
                    {
                        await Task.Delay(
                            TimeSpan.FromSeconds(TurnSeconds),
                            token
                        );

                        await GamesLock.WaitAsync();

                        try
                        {
                            // الحصول على لعبة نفس الروم
                            if (!Games.TryGetValue(
                                groupId,
                                out PenaltyGame? currentGame))
                            {
                                return;
                            }

                            if (!currentGame.Started)
                                return;

                            if (!currentGame.WaitingForShot)
                                return;

                            if (currentGame.TurnVersion != version)
                                return;

                            if (currentGame.CurrentPlayerIndex != playerIndex)
                                return;

                            var player =
                                currentGame.CurrentPlayer;

                            if (player == null)
                                return;

                            player.Shots++;

                            currentGame.WaitingForShot = false;

                            currentGame.TurnVersion++;

                            await SendToGroup(
                                currentGame.GroupId,
                                $"⏰ انتهى الوقت!\n\n" +
                                $"👤 اللاعب {player.Number}️⃣ لم يسدد.\n" +
                                $"❌ تم احتساب التسديدة كإضاعة.\n\n" +
                                $"🎯 التسديدات: {player.Shots}/{ShotsPerPlayer}"
                            );

                            if (AllShotsFinished(currentGame))
                            {
                                await Task.Delay(500);

                                await FinishGame(currentGame);

                                return;
                            }

                            MoveToNextPlayer(currentGame);

                            await Task.Delay(500);

                            await SendCurrentTurn(currentGame);

                            StartShotTimeout(currentGame);
                        }
                        finally
                        {
                            GamesLock.Release();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            "TIMEOUT ERROR:"
                        );

                        Console.WriteLine(ex);
                    }
                }
            );
        }

        // =========================================================
        // CANCEL TIMEOUT
        // =========================================================

        private static void CancelTurnTimeout(
            PenaltyGame game)
        {
            try
            {
                if (game.TurnTimeout != null)
                {
                    game.TurnTimeout.Cancel();

                    game.TurnTimeout.Dispose();

                    game.TurnTimeout = null;
                }
            }
            catch
            {
            }
        }

        // =========================================================
        // STATUS
        // =========================================================

        private static async Task ShowStatus(
            Message message,
            PenaltyGame game)
        {
            string text =
                "⚽ حالة لعبة ضربة الجزاء ⚽\n\n";

            foreach (var player in game.Players)
            {
                text +=
                    $"👤 اللاعب {player.Number}️⃣ — " +
                    $"⚽ {player.Goals} هدف — " +
                    $"🎯 {player.Shots}/{ShotsPerPlayer}\n";
            }

            if (game.Started &&
                game.CurrentPlayer != null)
            {
                text +=
                    $"\n🔥 الدور الحالي: " +
                    $"اللاعب {game.CurrentPlayer.Number}️⃣";
            }

            await Send(
                message,
                text
            );
        }

        // =========================================================
        // END GAME
        // =========================================================

        private static async Task EndGame(
            Message message,
            PenaltyGame game)
        {
            CancelTurnTimeout(game);

            Games.Remove(game.GroupId);

            await Send(
                message,
                "🛑 تم إنهاء لعبة ضربة الجزاء بهذا الروم."
            );
        }

        // =========================================================
        // FINISH GAME
        // =========================================================

        private static async Task FinishGame(
            PenaltyGame game)
        {
            CancelTurnTimeout(game);

            game.WaitingForShot = false;
            game.Started = false;

            var ranking =
                game.Players
                    .OrderByDescending(
                        p => p.Goals
                    )
                    .ThenBy(
                        p => p.Shots - p.Goals
                    )
                    .ToList();

            if (ranking.Count == 0)
            {
                Games.Remove(game.GroupId);
                return;
            }

            string text =
                "🏆⚽ انتهت لعبة ضربة الجزاء ⚽🏆\n\n";

            for (int i = 0;
                 i < ranking.Count;
                 i++)
            {
                var player =
                    ranking[i];

                string medal;

                if (i == 0)
                    medal = "🥇";
                else if (i == 1)
                    medal = "🥈";
                else if (i == 2)
                    medal = "🥉";
                else
                    medal = "🔹";

                text +=
                    $"{medal} اللاعب {player.Number}️⃣\n" +
                    $"⚽ الأهداف: {player.Goals}\n" +
                    $"🎯 التسديدات: {player.Shots}/{ShotsPerPlayer}\n\n";
            }

            int highest =
                ranking.First().Goals;

            var winners =
                ranking
                    .Where(
                        p => p.Goals == highest
                    )
                    .ToList();

            if (winners.Count == 1)
            {
                text +=
                    $"👑🏆 الفائز هو اللاعب " +
                    $"{winners[0].Number}️⃣!\n" +
                    $"⚽ برصيد {winners[0].Goals} أهداف 🔥";
            }
            else
            {
                text +=
                    "🤝 تعادل!\n\n" +
                    "🏆 الفائزون:\n";

                foreach (var winner in winners)
                {
                    text +=
                        $"👑 اللاعب {winner.Number}️⃣ — " +
                        $"{winner.Goals} أهداف\n";
                }
            }

            await SendToGroup(
                game.GroupId,
                text
            );

            Games.Remove(game.GroupId);
        }

        // =========================================================
        // DIRECTION
        // =========================================================

        private static string GetDirectionName(
            int direction)
        {
            return direction switch
            {
                1 => "يمين ➡️",
                2 => "وسط ⬆️",
                3 => "يسار ⬅️",
                _ => "غير معروف"
            };
        }

        // =========================================================
        // ARABIC NUMBER SUPPORT
        // =========================================================

        private static bool TryParseChoice(
            string text,
            out int number)
        {
            text =
                text
                    .Replace('٠', '0')
                    .Replace('١', '1')
                    .Replace('٢', '2')
                    .Replace('٣', '3')
                    .Replace('٤', '4')
                    .Replace('٥', '5')
                    .Replace('٦', '6')
                    .Replace('٧', '7')
                    .Replace('٨', '8')
                    .Replace('٩', '9');

            return int.TryParse(
                text,
                out number
            );
        }

        // =========================================================
        // REPLY
        // =========================================================

        private static async Task Send(
            Message message,
            string text)
        {
            try
            {
                await message.Reply(
                    Client,
                    text
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "SEND ERROR:"
                );

                Console.WriteLine(ex);
            }
        }

        // =========================================================
        // GROUP MESSAGE
        // =========================================================

        private static async Task SendToGroup(
            string groupId,
            string text)
        {
            try
            {
                await Client.GroupMessage(
                    groupId,
                    text
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "GROUP SEND ERROR:"
                );

                Console.WriteLine(ex);
            }
        }
    }
}
