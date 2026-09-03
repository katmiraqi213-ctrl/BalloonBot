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

        private static readonly string TargetGroupId = "82041031";

        private static PenaltyGame? Game;

        private static readonly SemaphoreSlim GameLock =
            new SemaphoreSlim(1, 1);

        private static readonly Random Random =
            new Random();

        private const int MaxPlayers = 10;
        private const int ShotsPerPlayer = 5;
        private const int TurnSeconds = 10;

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

            try
            {
                bool subscribed =
                    await Client.Messaging.GroupMessageSubscribe(
                        TargetGroupId
                    );

                Console.WriteLine(
                    subscribed
                        ? $"SUBSCRIBED TO GROUP: {TargetGroupId}"
                        : "GROUP SUBSCRIBE FAILED"
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine("SUBSCRIBE ERROR:");
                Console.WriteLine(ex);

                return;
            }

            Console.WriteLine("=================================");
            Console.WriteLine("BOT IS ONLINE");
            Console.WriteLine($"GROUP: {TargetGroupId}");
            Console.WriteLine("WAITING FOR MESSAGES...");
            Console.WriteLine("=================================");

            await Task.Delay(Timeout.Infinite);
        }

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
                    $"MESSAGE RECEIVED | Group={message.GroupId} | Content={message.Content}"
                );

                if (!message.IsGroup)
                    return;

                if (message.GroupId != TargetGroupId)
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

        private static async Task HandleMessage(
            Message message,
            string content)
        {
            await GameLock.WaitAsync();

            try
            {
                string lower =
                    content.ToLowerInvariant();

                // =========================
                // HELP
                // =========================

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

                // =========================
                // CREATE GAME
                // =========================

                if (lower == "!جزاء")
                {
                    await NewGame(message);
                    return;
                }

                // =========================
                // NO GAME
                // =========================

                if (Game == null)
                {
                    if (lower.StartsWith("!جزاء"))
                    {
                        await Send(
                            message,
                            "❌ لا توجد لعبة حالياً.\n" +
                            "اكتب !جزاء لإنشاء لعبة جديدة."
                        );
                    }

                    return;
                }

                // =========================
                // JOIN
                // =========================

                if (lower == "!جزاء انضم" ||
                    lower == "!جزاء انضمام")
                {
                    await JoinGame(message);
                    return;
                }

                // =========================
                // PLAYERS
                // =========================

                if (lower == "!جزاء لاعبين")
                {
                    await ShowPlayers(message);
                    return;
                }

                // =========================
                // START
                // =========================

                if (lower == "!جزاء بدء")
                {
                    await StartGame(message);
                    return;
                }

                // =========================
                // STATUS
                // =========================

                if (lower == "!جزاء حالة")
                {
                    await ShowStatus(message);
                    return;
                }

                // =========================
                // END
                // =========================

                if (lower == "!جزاء انهاء" ||
                    lower == "!جزاء إنهاء")
                {
                    await EndGame(message);
                    return;
                }

                // =========================
                // GAMEPLAY
                // =========================

                if (Game.Started &&
                    Game.WaitingForShot)
                {
                    if (TryParseChoice(
                        content,
                        out int choice))
                    {
                        await ProcessShot(
                            message,
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
                GameLock.Release();
            }
        }

        // =========================================================
        // NEW GAME
        // =========================================================

        private static async Task NewGame(
            Message message)
        {
            if (Game != null)
            {
                await Send(
                    message,
                    "⚠️ توجد لعبة بالفعل.\n" +
                    "اكتب !جزاء لاعبين لمعرفة اللاعبين."
                );

                return;
            }

            Game = new PenaltyGame
            {
                GroupId = TargetGroupId,
                Started = false,
                CurrentPlayerIndex = 0,
                WaitingForShot = false
            };

            await Send(
                message,
                "⚽🔥 تم إنشاء لعبة ضربة الجزاء!\n\n" +
                "للانضمام اكتب:\n" +
                "!جزاء انضم\n\n" +
                "👥 الحد الأقصى: 10 لاعبين\n" +
                "🎯 كل لاعب لديه 5 تسديدات\n" +
                "⏱️ 10 ثواني لكل تسديدة\n\n" +
                "عند اكتمال اللاعبين اكتب:\n" +
                "!جزاء بدء"
            );
        }

        // =========================================================
        // JOIN
        // =========================================================

        private static async Task JoinGame(
            Message message)
        {
            if (Game == null)
                return;

            if (Game.Started)
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

            if (Game.Players.Any(
                p => p.UserId == userId))
            {
                var existing =
                    Game.Players.First(
                        p => p.UserId == userId
                    );

                await Send(
                    message,
                    $"⚠️ أنت مسجل بالفعل كـ اللاعب {existing.Number}️⃣."
                );

                return;
            }

            if (Game.Players.Count >= MaxPlayers)
            {
                await Send(
                    message,
                    "❌ اللعبة مكتملة، الحد الأقصى 10 لاعبين."
                );

                return;
            }

            int number =
                Game.Players.Count + 1;

            var player =
                new PenaltyPlayer
                {
                    UserId = userId,
                    Number = number
                };

            Game.Players.Add(player);

            await Send(
                message,
                $"✅ تم انضمامك إلى اللعبة!\n\n" +
                $"👤 رقمك: اللاعب {number}️⃣\n" +
                $"👥 عدد اللاعبين: {Game.Players.Count}/{MaxPlayers}\n\n" +
                "عند جاهزية اللاعبين اكتب:\n" +
                "!جزاء بدء"
            );
        }

        // =========================================================
        // SHOW PLAYERS
        // =========================================================

        private static async Task ShowPlayers(
            Message message)
        {
            if (Game == null)
                return;

            if (Game.Players.Count == 0)
            {
                await Send(
                    message,
                    "👥 لا يوجد لاعبون حالياً."
                );

                return;
            }

            string text =
                "⚽ لاعبو لعبة ضربة الجزاء ⚽\n\n";

            foreach (var player in Game.Players)
            {
                text +=
                    $"👤 اللاعب {player.Number}️⃣\n" +
                    $"🎯 الأهداف: {player.Goals}\n" +
                    $"🥅 التسديدات: {player.Shots}/{ShotsPerPlayer}\n\n";
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
            Message message)
        {
            if (Game == null)
                return;

            if (Game.Started)
            {
                await Send(
                    message,
                    "⚠️ اللعبة بدأت بالفعل."
                );

                return;
            }

            if (Game.Players.Count < 2)
            {
                await Send(
                    message,
                    "❌ يجب أن يكون هناك لاعبان على الأقل لبدء اللعبة."
                );

                return;
            }

            Game.Started = true;

            Game.CurrentPlayerIndex = 0;

            Game.WaitingForShot = true;

            Game.TurnVersion++;

            await Send(
                message,
                "⚽🔥 بدأت لعبة ضربة الجزاء!\n\n" +
                $"👥 عدد اللاعبين: {Game.Players.Count}\n" +
                $"🎯 كل لاعب لديه {ShotsPerPlayer} تسديدات\n" +
                $"⏱️ الوقت: {TurnSeconds} ثواني\n\n" +
                "🏆 اللاعب صاحب أكبر عدد من الأهداف يفوز!"
            );

            await Task.Delay(500);

            await SendCurrentTurn();

            StartShotTimeout();
        }

        // =========================================================
        // SEND CURRENT TURN
        // =========================================================

        private static async Task SendCurrentTurn()
        {
            if (Game == null ||
                !Game.Started ||
                !Game.WaitingForShot)
                return;

            var player =
                Game.CurrentPlayer;

            if (player == null)
                return;

            int shotNumber =
                player.Shots + 1;

            await SendToGroup(
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
            int choice)
        {
            if (Game == null ||
                !Game.Started ||
                !Game.WaitingForShot)
                return;

            var player =
                Game.CurrentPlayer;

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

            CancelTurnTimeout();

            Game.WaitingForShot = false;

            Game.TurnVersion++;

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
                    $"🧤❌ تــــــم التصدي!\n\n" +
                    $"👤 اللاعب {player.Number}️⃣\n" +
                    $"🎯 التسديدة: {shotName}\n" +
                    $"🧤 الحارس اختار: {goalkeeperName}\n\n" +
                    "الحارس قرأ التسديدة! 🔥"
                );
            }

            if (AllShotsFinished())
            {
                await Task.Delay(700);

                await FinishGame();

                return;
            }

            MoveToNextPlayer();

            await Task.Delay(500);

            await SendCurrentTurn();

            StartShotTimeout();
        }

        // =========================================================
        // MOVE NEXT PLAYER
        // =========================================================

        private static void MoveToNextPlayer()
        {
            if (Game == null ||
                Game.Players.Count == 0)
                return;

            int count =
                Game.Players.Count;

            for (int i = 1; i <= count; i++)
            {
                int index =
                    (Game.CurrentPlayerIndex + i)
                    % count;

                var player =
                    Game.Players[index];

                if (player.Shots < ShotsPerPlayer)
                {
                    Game.CurrentPlayerIndex =
                        index;

                    Game.WaitingForShot = true;

                    Game.TurnVersion++;

                    return;
                }
            }
        }

        // =========================================================
        // ALL SHOTS FINISHED
        // =========================================================

        private static bool AllShotsFinished()
        {
            if (Game == null)
                return true;

            return Game.Players.All(
                p => p.Shots >= ShotsPerPlayer
            );
        }

        // =========================================================
        // TIMEOUT
        // =========================================================

        private static void StartShotTimeout()
        {
            if (Game == null)
                return;

            CancelTurnTimeout();

            Game.TurnTimeout =
                new CancellationTokenSource();

            CancellationToken token =
                Game.TurnTimeout.Token;

            int version =
                Game.TurnVersion;

            int playerIndex =
                Game.CurrentPlayerIndex;

            _ = Task.Run(
                async () =>
                {
                    try
                    {
                        await Task.Delay(
                            TimeSpan.FromSeconds(TurnSeconds),
                            token
                        );

                        await GameLock.WaitAsync();

                        try
                        {
                            if (Game == null)
                                return;

                            if (!Game.Started)
                                return;

                            if (!Game.WaitingForShot)
                                return;

                            if (Game.TurnVersion != version)
                                return;

                            if (Game.CurrentPlayerIndex != playerIndex)
                                return;

                            var player =
                                Game.CurrentPlayer;

                            if (player == null)
                                return;

                            player.Shots++;

                            Game.WaitingForShot = false;

                            Game.TurnVersion++;

                            await SendToGroup(
                                $"⏰ انتهى الوقت!\n\n" +
                                $"👤 اللاعب {player.Number}️⃣ لم يسدد.\n" +
                                $"❌ تم احتساب التسديدة كإضاعة.\n\n" +
                                $"🎯 التسديدات: {player.Shots}/{ShotsPerPlayer}"
                            );

                            if (AllShotsFinished())
                            {
                                await Task.Delay(500);

                                await FinishGame();

                                return;
                            }

                            MoveToNextPlayer();

                            await Task.Delay(500);

                            await SendCurrentTurn();

                            StartShotTimeout();
                        }
                        finally
                        {
                            GameLock.Release();
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

        private static void CancelTurnTimeout()
        {
            try
            {
                if (Game?.TurnTimeout != null)
                {
                    Game.TurnTimeout.Cancel();
                    Game.TurnTimeout.Dispose();
                    Game.TurnTimeout = null;
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
            Message message)
        {
            if (Game == null)
            {
                await Send(
                    message,
                    "❌ لا توجد لعبة حالياً."
                );

                return;
            }

            string text =
                "⚽ حالة لعبة ضربة الجزاء ⚽\n\n";

            foreach (var player in Game.Players)
            {
                text +=
                    $"👤 اللاعب {player.Number}️⃣ — " +
                    $"⚽ {player.Goals} هدف — " +
                    $"🎯 {player.Shots}/{ShotsPerPlayer}\n";
            }

            if (Game.Started &&
                Game.CurrentPlayer != null)
            {
                text +=
                    $"\n🔥 الدور الحالي: " +
                    $"اللاعب {Game.CurrentPlayer.Number}️⃣";
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
            Message message)
        {
            if (Game == null)
            {
                await Send(
                    message,
                    "❌ لا توجد لعبة لإنهائها."
                );

                return;
            }

            CancelTurnTimeout();

            Game = null;

            await Send(
                message,
                "🛑 تم إنهاء لعبة ضربة الجزاء."
            );
        }

        // =========================================================
        // FINISH GAME
        // =========================================================

        private static async Task FinishGame()
        {
            if (Game == null)
                return;

            CancelTurnTimeout();

            Game.WaitingForShot = false;
            Game.Started = false;

            var ranking =
                Game.Players
                    .OrderByDescending(
                        p => p.Goals
                    )
                    .ThenBy(
                        p => p.Shots - p.Goals
                    )
                    .ToList();

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

            await SendToGroup(text);

            Game = null;
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
            string text)
        {
            try
            {
                await Client.GroupMessage(
                    TargetGroupId,
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
