using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WolfLive.Api;
using WolfLive.Api.Models;

namespace BalloonBot
{
    public class Player
    {
        public string UserId { get; set; } = "";
        public int Number { get; set; }
        public int Balloons { get; set; } = 7;
        public bool Eliminated { get; set; } = false;
    }

    public class BalloonGame
    {
        public string GroupId { get; set; } = "";

        public List<Player> Players { get; set; } = new List<Player>();

        public bool Started { get; set; } = false;

        public int CurrentPlayerIndex { get; set; } = 0;

        public bool WaitingForOpponent { get; set; } = false;

        public bool WaitingForBalloon { get; set; } = false;

        public Player? SelectedOpponent { get; set; }

        public CancellationTokenSource? TurnTimeout { get; set; }

        public Player? CurrentPlayer
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

        private static BalloonGame? Game;

        private static readonly SemaphoreSlim GameLock =
            new SemaphoreSlim(1, 1);

        public static async Task Main(string[] args)
        {
            string email =
                Environment.GetEnvironmentVariable("WOLF_EMAIL") ?? "";

            string password =
                Environment.GetEnvironmentVariable("WOLF_PASSWORD") ?? "";

            Console.WriteLine("=================================");
            Console.WriteLine("       BALLOON BOT STARTING");
            Console.WriteLine("=================================");

            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                Console.WriteLine("ERROR: WOLF_EMAIL or WOLF_PASSWORD missing.");
                return;
            }

            Client = new WolfClient();

            // نستقبل الرسائل مباشرة من socket
            // بدون Messaging.Initialize()
            Client.On<WolfMessage>(
                "message send",
                OnWolfMessage
            );

            bool loggedIn = await LoginManually(email, password);

            if (!loggedIn)
            {
                Console.WriteLine("LOGIN FAILED.");
                return;
            }

            Console.WriteLine("LOGIN SUCCESS");

            try
            {
                bool subscribed =
                    await Client.Messaging.GroupMessageSubscribe(TargetGroupId);

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

                // ننتظر welcome قبل تسجيل الدخول
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

                Console.WriteLine("Connected to WOLF server.");
                Console.WriteLine("Waiting for WOLF welcome...");

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

                var user = await Client.Emit<User>(
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
                    Console.WriteLine("LOGIN RESPONSE IS NULL.");
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

                // نحول الرسالة إلى Message
                var message = new Message(wolfMessage);

                // نطبع كل رسالة تصل للـ Log للتشخيص
                Console.WriteLine(
                    $"MESSAGE RECEIVED | Group={message.GroupId} | Content={message.Content}"
                );

                // فقط الكروب المطلوب
                if (!message.IsGroup)
                    return;

                if (message.GroupId != TargetGroupId)
                    return;

                string content =
                    (message.Content ?? "").Trim();

                if (string.IsNullOrWhiteSpace(content))
                    return;

                await HandleMessage(message, content);
            }
            catch (Exception ex)
            {
                // مهم جداً حتى لا ينهار البوت
                Console.WriteLine("MESSAGE ERROR:");
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
                // =========================
                // أوامر البوت
                // =========================

                if (content.Equals(
                        "!بالونات",
                        StringComparison.OrdinalIgnoreCase)
                    ||
                    content.Equals(
                        "!بالونات مساعدة",
                        StringComparison.OrdinalIgnoreCase))
                {
                    await Send(
                        message,
                        "🎈 أوامر لعبة البالونات 🎈\n\n" +
                        "!بالونات جديد — إنشاء لعبة\n" +
                        "!بالونات انضم — الانضمام\n" +
                        "!بالونات لاعبين — عرض اللاعبين\n" +
                        "!بالونات بدء — بدء اللعبة\n" +
                        "!بالونات انهاء — إنهاء اللعبة\n\n" +
                        "🎈 كل لاعب يبدأ بـ 7 بالونات."
                    );

                    return;
                }

                if (content.Equals(
                        "!بالونات جديد",
                        StringComparison.OrdinalIgnoreCase))
                {
                    await NewGame(message);
                    return;
                }

                if (content.Equals(
                        "!بالونات انضم",
                        StringComparison.OrdinalIgnoreCase)
                    ||
                    content.Equals(
                        "!بالونات انضمام",
                        StringComparison.OrdinalIgnoreCase))
                {
                    await JoinGame(message);
                    return;
                }

                if (content.Equals(
                        "!بالونات لاعبين",
                        StringComparison.OrdinalIgnoreCase))
                {
                    await ShowPlayers(message);
                    return;
                }

                if (content.Equals(
                        "!بالونات بدء",
                        StringComparison.OrdinalIgnoreCase))
                {
                    await StartGame(message);
                    return;
                }

                if (content.Equals(
                        "!بالونات انهاء",
                        StringComparison.OrdinalIgnoreCase)
                    ||
                    content.Equals(
                        "!بالونات إنهاء",
                        StringComparison.OrdinalIgnoreCase))
                {
                    await EndGame(message);
                    return;
                }

                // =========================
                // أرقام اللعبة
                // =========================

                if (Game == null)
                    return;

                if (!Game.Started)
                    return;

                // نتأكد أن الرسالة من اللاعب الحالي
                Player? current = Game.CurrentPlayer;

                if (current == null)
                    return;

                if (message.UserId != current.UserId)
                    return;

                // إذا ننتظر اختيار الخصم
                if (Game.WaitingForOpponent)
                {
                    if (int.TryParse(content, out int opponentNumber))
                    {
                        await ChooseOpponent(
                            message,
                            opponentNumber
                        );
                    }

                    return;
                }

                // إذا ننتظر اختيار البالونة
                if (Game.WaitingForBalloon)
                {
                    if (int.TryParse(content, out int balloonNumber))
                    {
                        await ChooseBalloon(
                            message,
                            balloonNumber
                        );
                    }

                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("HANDLE ERROR:");
                Console.WriteLine(ex);
            }
            finally
            {
                GameLock.Release();
            }
        }

        // =====================================================
        // إنشاء اللعبة
        // =====================================================

        private static async Task NewGame(Message message)
        {
            if (Game != null)
            {
                await Send(
                    message,
                    "⚠️ توجد لعبة بالونات حالياً.\n" +
                    "استخدم !بالونات انهاء أولاً."
                );

                return;
            }

            Game = new BalloonGame
            {
                GroupId = TargetGroupId
            };

            await Send(
                message,
                "🎈🔥 تم إنشاء لعبة البالونات 🔥🎈\n\n" +
                "كل لاعب يبدأ بـ 7 🎈\n\n" +
                "للانضمام أرسل:\n" +
                "!بالونات انضم\n\n" +
                "بعد اكتمال اللاعبين:\n" +
                "!بالونات بدء"
            );
        }

        // =====================================================
        // الانضمام
        // =====================================================

        private static async Task JoinGame(Message message)
        {
            if (Game == null)
            {
                await Send(
                    message,
                    "❌ لا توجد لعبة حالياً.\n" +
                    "اكتب !بالونات جديد"
                );

                return;
            }

            if (Game.Started)
            {
                await Send(
                    message,
                    "❌ اللعبة بدأت بالفعل."
                );

                return;
            }

            if (Game.Players.Any(
                p => p.UserId == message.UserId))
            {
                await Send(
                    message,
                    "⚠️ أنت منضم للعبة بالفعل."
                );

                return;
            }

            int number = Game.Players.Count + 1;

            Game.Players.Add(
                new Player
                {
                    UserId = message.UserId,
                    Number = number,
                    Balloons = 7
                }
            );

            await Send(
                message,
                $"🎈 تم انضمام اللاعب رقم {number}\n" +
                $"رصيده: 7 🎈\n\n" +
                $"عدد اللاعبين: {Game.Players.Count}"
            );
        }

        // =====================================================
        // عرض اللاعبين
        // =====================================================

        private static async Task ShowPlayers(Message message)
        {
            if (Game == null ||
                Game.Players.Count == 0)
            {
                await Send(
                    message,
                    "❌ لا يوجد لاعبين حالياً."
                );

                return;
            }

            string text =
                "🎈 قائمة اللاعبين 🎈\n\n";

            foreach (var player in Game.Players)
            {
                string balloons =
                    string.Concat(
                        Enumerable.Repeat(
                            "🎈",
                            Math.Min(
                                player.Balloons,
                                7
                            )
                        )
                    );

                string status =
                    player.Eliminated
                        ? " ❌ خارج اللعبة"
                        : "";

                text +=
                    $"{player.Number}️⃣ لاعب {player.Number} — " +
                    $"{player.Balloons} {balloons}" +
                    $"{status}\n";
            }

            if (Game.Started &&
                Game.CurrentPlayer != null)
            {
                text +=
                    $"\n🎯 الدور على اللاعب " +
                    $"{Game.CurrentPlayer.Number}️⃣";
            }

            await Send(message, text);
        }

        // =====================================================
        // بدء اللعبة
        // =====================================================

        private static async Task StartGame(Message message)
        {
            if (Game == null)
            {
                await Send(
                    message,
                    "❌ لا توجد لعبة."
                );

                return;
            }

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
                    "❌ تحتاج اللعبة إلى لاعبين اثنين على الأقل."
                );

                return;
            }

            Game.Started = true;
            Game.CurrentPlayerIndex = 0;
            Game.WaitingForOpponent = true;
            Game.WaitingForBalloon = false;

            Player? current = Game.CurrentPlayer;

            await Send(
                message,
                "🎈🔥 بدأت لعبة البالونات 🔥🎈\n\n" +
                $"🎯 الدور على اللاعب {current?.Number}️⃣\n\n" +
                "أرسل رقم اللاعب الذي تريد مهاجمته.\n" +
                "⏱️ لديك 15 ثانية."
            );

            StartOpponentTimeout();
        }

        // =====================================================
        // اختيار الخصم
        // =====================================================

        private static async Task ChooseOpponent(
            Message message,
            int opponentNumber)
        {
            if (Game == null)
                return;

            Player? current =
                Game.CurrentPlayer;

            if (current == null)
                return;

            Player? opponent =
                Game.Players.FirstOrDefault(
                    p => p.Number == opponentNumber
                );

            if (opponent == null)
            {
                await Send(
                    message,
                    "❌ رقم اللاعب غير صحيح."
                );

                return;
            }

            if (opponent.UserId == current.UserId)
            {
                await Send(
                    message,
                    "❌ لا يمكنك اختيار نفسك."
                );

                return;
            }

            if (opponent.Eliminated ||
                opponent.Balloons <= 0)
            {
                await Send(
                    message,
                    "❌ هذا اللاعب خرج من اللعبة."
                );

                return;
            }

            Game.TurnTimeout?.Cancel();

            Game.SelectedOpponent = opponent;

            Game.WaitingForOpponent = false;
            Game.WaitingForBalloon = true;

            await Send(
                message,
                $"🎯 اخترت اللاعب {opponent.Number}️⃣\n\n" +
                $"عنده حالياً {opponent.Balloons} 🎈\n\n" +
                $"أرسل رقم البالونة من 1 إلى {opponent.Balloons}\n" +
                "⏱️ لديك 15 ثانية."
            );

            StartBalloonTimeout();
        }

        // =====================================================
        // اختيار البالونة
        // =====================================================

        private static async Task ChooseBalloon(
            Message message,
            int balloonNumber)
        {
            if (Game == null)
                return;

            Player? current =
                Game.CurrentPlayer;

            Player? opponent =
                Game.SelectedOpponent;

            if (current == null ||
                opponent == null)
                return;

            if (balloonNumber < 1 ||
                balloonNumber > opponent.Balloons)
            {
                await Send(
                    message,
                    $"❌ رقم البالونة غير صحيح.\n" +
                    $"اختر من 1 إلى {opponent.Balloons}"
                );

                return;
            }

            Game.TurnTimeout?.Cancel();

            Game.WaitingForBalloon = false;

            Random random = new Random();

            int chance = random.Next(1, 101);

            // 15% حظ
            if (chance <= 15)
            {
                await Send(
                    message,
                    $"🍀 حظ!\n\n" +
                    $"البالونة رقم {balloonNumber}️⃣ " +
                    "لم تنفجر!\n\n" +
                    $"اللاعب {opponent.Number}️⃣ ما زال عنده " +
                    $"{opponent.Balloons} 🎈"
                );

                Game.SelectedOpponent = null;

                MoveToNextPlayer();

                return;
            }

            // 15% نجاة
            if (chance <= 30)
            {
                await Send(
                    message,
                    $"🛡️ نجاة!\n\n" +
                    $"البالونة رقم {balloonNumber}️⃣ " +
                    "بقيت بدون انفجار!\n\n" +
                    $"اللاعب {opponent.Number}️⃣ عنده " +
                    $"{opponent.Balloons} 🎈"
                );

                Game.SelectedOpponent = null;

                MoveToNextPlayer();

                return;
            }

            // 10% دور إضافي
            if (chance <= 40)
            {
                opponent.Balloons--;

                string result =
                    $"🔄 دور إضافي!\n\n" +
                    $"💥 انفجرت البالونة رقم {balloonNumber}️⃣\n" +
                    $"اللاعب {opponent.Number}️⃣ أصبح عنده " +
                    $"{opponent.Balloons} 🎈";

                if (opponent.Balloons <= 0)
                {
                    opponent.Balloons = 0;
                    opponent.Eliminated = true;

                    result +=
                        $"\n\n❌ اللاعب {opponent.Number}️⃣ خرج من اللعبة!";

                    if (CheckWinner(out Player? winner))
                    {
                        await Send(
                            message,
                            result +
                            $"\n\n🏆 الفائز هو اللاعب " +
                            $"{winner!.Number}️⃣ 🎉"
                        );

                        FinishGame();

                        return;
                    }
                }

                Game.SelectedOpponent = null;
                Game.WaitingForOpponent = true;

                await Send(
                    message,
                    result +
                    "\n\n🔥 لديك دور إضافي!\n" +
                    $"🎯 اللاعب {current.Number}️⃣ اختر خصماً جديداً.\n" +
                    "⏱️ لديك 15 ثانية."
                );

                StartOpponentTimeout();

                return;
            }

            // 60% انفجار طبيعي
            opponent.Balloons--;

            if (opponent.Balloons < 0)
                opponent.Balloons = 0;

            string normalResult =
                $"💥 انفجرت البالونة رقم {balloonNumber}️⃣!\n\n" +
                $"اللاعب {opponent.Number}️⃣ بقي عنده " +
                $"{opponent.Balloons} 🎈";

            if (opponent.Balloons == 0)
            {
                opponent.Eliminated = true;

                normalResult +=
                    $"\n\n❌ اللاعب {opponent.Number}️⃣ خرج من اللعبة!";

                if (CheckWinner(out Player? winner))
                {
                    await Send(
                        message,
                        normalResult +
                        $"\n\n🏆🎉 الفائز هو اللاعب " +
                        $"{winner!.Number}️⃣ 🎉🏆"
                    );

                    FinishGame();

                    return;
                }
            }

            Game.SelectedOpponent = null;

            await Send(
                message,
                normalResult
            );

            MoveToNextPlayer();
        }

        // =====================================================
        // الانتقال للاعب التالي
        // =====================================================

        private static void MoveToNextPlayer()
        {
            if (Game == null)
                return;

            int count = Game.Players.Count;

            for (int i = 1; i <= count; i++)
            {
                int nextIndex =
                    (Game.CurrentPlayerIndex + i) % count;

                Player next =
                    Game.Players[nextIndex];

                if (!next.Eliminated &&
                    next.Balloons > 0)
                {
                    Game.CurrentPlayerIndex =
                        nextIndex;

                    Game.WaitingForOpponent = true;
                    Game.WaitingForBalloon = false;

                    SendCurrentTurn();

                    StartOpponentTimeout();

                    return;
                }
            }
        }

        // =====================================================
        // إرسال الدور الحالي
        // =====================================================

        private static async void SendCurrentTurn()
        {
            try
            {
                if (Game == null)
                    return;

                Player? current =
                    Game.CurrentPlayer;

                if (current == null)
                    return;

                await SendToGroup(
                    $"🎯 الدور الآن على اللاعب " +
                    $"{current.Number}️⃣\n\n" +
                    "اختر رقم اللاعب الذي تريد مهاجمته.\n" +
                    "⏱️ لديك 15 ثانية."
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "TURN MESSAGE ERROR:"
                );

                Console.WriteLine(ex);
            }
        }

        // =====================================================
        // مؤقت اختيار الخصم
        // =====================================================

        private static void StartOpponentTimeout()
        {
            if (Game == null)
                return;

            Game.TurnTimeout?.Cancel();

            Game.TurnTimeout =
                new CancellationTokenSource();

            CancellationToken token =
                Game.TurnTimeout.Token;

            _ = Task.Run(
                async () =>
                {
                    try
                    {
                        await Task.Delay(
                            TimeSpan.FromSeconds(15),
                            token
                        );

                        if (token.IsCancellationRequested)
                            return;

                        await GameLock.WaitAsync();

                        try
                        {
                            if (Game == null ||
                                !Game.Started ||
                                !Game.WaitingForOpponent)
                                return;

                            Player? current =
                                Game.CurrentPlayer;

                            if (current == null)
                                return;

                            Game.WaitingForOpponent = false;

                            await SendToGroup(
                                $"⏰ انتهى الوقت!\n\n" +
                                $"تم تخطي دور اللاعب " +
                                $"{current.Number}️⃣."
                            );

                            MoveToNextPlayer();
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
                            "OPPONENT TIMEOUT ERROR:"
                        );

                        Console.WriteLine(ex);
                    }
                }
            );
        }

        // =====================================================
        // مؤقت اختيار البالونة
        // =====================================================

        private static void StartBalloonTimeout()
        {
            if (Game == null)
                return;

            Game.TurnTimeout?.Cancel();

            Game.TurnTimeout =
                new CancellationTokenSource();

            CancellationToken token =
                Game.TurnTimeout.Token;

            _ = Task.Run(
                async () =>
                {
                    try
                    {
                        await Task.Delay(
                            TimeSpan.FromSeconds(15),
                            token
                        );

                        if (token.IsCancellationRequested)
                            return;

                        await GameLock.WaitAsync();

                        try
                        {
                            if (Game == null ||
                                !Game.Started ||
                                !Game.WaitingForBalloon)
                                return;

                            Player? current =
                                Game.CurrentPlayer;

                            Game.WaitingForBalloon = false;
                            Game.SelectedOpponent = null;

                            if (current != null)
                            {
                                await SendToGroup(
                                    $"⏰ انتهى الوقت!\n\n" +
                                    $"تم تخطي دور اللاعب " +
                                    $"{current.Number}️⃣."
                                );
                            }

                            MoveToNextPlayer();
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
                            "BALLOON TIMEOUT ERROR:"
                        );

                        Console.WriteLine(ex);
                    }
                }
            );
        }

        // =====================================================
        // التحقق من الفائز
        // =====================================================

        private static bool CheckWinner(
            out Player? winner)
        {
            winner =
                Game?.Players.FirstOrDefault(
                    p =>
                        !p.Eliminated &&
                        p.Balloons > 0
                );

            if (winner == null)
                return false;

            int alive =
                Game!.Players.Count(
                    p =>
                        !p.Eliminated &&
                        p.Balloons > 0
                );

            return alive == 1;
        }

        // =====================================================
        // إنهاء اللعبة
        // =====================================================

        private static async Task EndGame(Message message)
        {
            if (Game == null)
            {
                await Send(
                    message,
                    "❌ لا توجد لعبة حالياً."
                );

                return;
            }

            Game.TurnTimeout?.Cancel();

            Game = null;

            await Send(
                message,
                "🛑 تم إنهاء لعبة البالونات."
            );
        }

        // =====================================================
        // إنهاء داخلي بعد الفوز
        // =====================================================

        private static void FinishGame()
        {
            if (Game == null)
                return;

            Game.TurnTimeout?.Cancel();

            Game.Started = false;
        }

        // =====================================================
        // إرسال رد
        // =====================================================

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

        // =====================================================
        // إرسال مباشر للكروب
        // =====================================================

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
