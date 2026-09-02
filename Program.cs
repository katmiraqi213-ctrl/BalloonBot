using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WolfLive.Api;
using WolfLive.Api.Models;

namespace BalloonBot
{
    public class Program
    {
        // =========================================================
        // إعدادات البوت
        // =========================================================

        public const string TargetGroupId = "82041031";

        private const string MessageSendEvent = "message send";
        private const string SecurityLoginEvent = "security login";

        private static IWolfClient Client = null!;

        private static BalloonGame? Game;

        private static readonly SemaphoreSlim MessageLock =
            new SemaphoreSlim(1, 1);

        public static async Task Main(string[] args)
        {
            Console.WriteLine("====================================");
            Console.WriteLine("       BalloonBot - WOLF");
            Console.WriteLine("====================================");

            string? email = Environment.GetEnvironmentVariable("WOLF_EMAIL");
            string? password = Environment.GetEnvironmentVariable("WOLF_PASSWORD");

            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                Console.WriteLine("ERROR: WOLF_EMAIL or WOLF_PASSWORD is missing.");
                return;
            }

            try
            {
                // إنشاء الاتصال
                Client = new WolfClient();

                // =================================================
                // مهم جداً:
                // لا نستخدم SetupCommands()
                // ولا نستخدم Client.Messaging.Initialize()
                //
                // لأن Initialize() يشترك بكل الكروبات ويجلب
                // GetGroupUser() لكل رسالة.
                // =================================================

                Client.On<WolfMessage>(
                    MessageSendEvent,
                    OnWolfMessage
                );

                // تسجيل الدخول يدوياً حتى لا يتم تشغيل
                // WolfMessaging.Initialize()
                bool loggedIn = await LoginManually(email, password);

                if (!loggedIn)
                {
                    Console.WriteLine("Login FAILED.");
                    return;
                }

                Console.WriteLine("Login SUCCESS.");
                Console.WriteLine("Target group: " + TargetGroupId);

                // الاشتراك فقط بالكروب المطلوب
                try
                {
                    bool subscribed =
                        await Client.Messaging.GroupMessageSubscribe(
                            TargetGroupId
                        );

                    Console.WriteLine(
                        "Target group subscribe: " +
                        (subscribed ? "SUCCESS" : "FAILED")
                    );
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        "Subscribe error: " + ex.Message
                    );
                }

                Console.WriteLine("BalloonBot is ONLINE.");
                Console.WriteLine("Waiting for messages...");

                // إبقاء البوت يعمل
                await Task.Delay(Timeout.Infinite);
            }
            catch (Exception ex)
            {
                Console.WriteLine("MAIN ERROR:");
                Console.WriteLine(ex);
            }
        }

        // =========================================================
        // تسجيل الدخول اليدوي
        // =========================================================

        private static async Task<bool> LoginManually(
            string email,
            string password)
        {
            try
            {
                await Client.Connect();

                Console.WriteLine("Connected to WOLF server.");

                var user = await Client.Emit<User>(
                    new Packet(
                        SecurityLoginEvent,
                        new
                        {
                            username = email,
                            password = password
                        }
                    )
                );

                if (user == null)
                {
                    return false;
                }

                // نخزن بيانات الحساب فقط.
                // لا نشغل Profiling.Initialize()
                // ولا Messaging.Initialize()
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
        // استقبال رسائل WOLF مباشرة
        // =========================================================

        private static void OnWolfMessage(WolfMessage wolfMessage)
        {
            try
            {
                // تحويل الرسالة إلى Message
                var message = new Message(wolfMessage);

                // نهتم فقط برسائل الكروب المطلوب
                if (!message.IsGroup)
                    return;

                if (message.GroupId != TargetGroupId)
                    return;

                // تشغيل المعالجة بالخلفية
                _ = ProcessMessageSafe(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "MESSAGE CALLBACK ERROR: " + ex.Message
                );
            }
        }

        // =========================================================
        // معالجة الرسائل بأمان
        // =========================================================

        private static async Task ProcessMessageSafe(Message message)
        {
            await MessageLock.WaitAsync();

            try
            {
                await ProcessMessage(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("PROCESS MESSAGE ERROR:");
                Console.WriteLine(ex);
            }
            finally
            {
                MessageLock.Release();
            }
        }

        // =========================================================
        // معالجة أوامر البوت
        // =========================================================

        private static async Task ProcessMessage(Message message)
        {
            string text = (message.Content ?? "").Trim();

            if (string.IsNullOrWhiteSpace(text))
                return;

            Console.WriteLine(
                $"[{message.GroupId}] [{message.UserId}] {text}"
            );

            // -----------------------------------------------------
            // أوامر البالونات
            // -----------------------------------------------------

            if (text.StartsWith("!بالونات", StringComparison.OrdinalIgnoreCase))
            {
                await HandleBalloonCommand(message, text);
                return;
            }

            // -----------------------------------------------------
            // أثناء اللعبة:
            // الأرقام فقط تعتبر اختيارات
            // -----------------------------------------------------

            if (Game != null &&
                Game.IsRunning &&
                message.GroupId == TargetGroupId &&
                int.TryParse(text, out int number))
            {
                await Game.HandleNumber(
                    message.UserId,
                    number,
                    message
                );
            }
        }

        // =========================================================
        // أوامر !بالونات
        // =========================================================

        private static async Task HandleBalloonCommand(
            Message message,
            string text)
        {
            string command = text.Trim();

            string[] parts = command
                .Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries
                );

            string subCommand =
                parts.Length >= 2
                    ? parts[1].Trim()
                    : "";

            // -----------------------------------------------------
            // !بالونات
            // !بالونات مساعدة
            // -----------------------------------------------------

            if (string.IsNullOrWhiteSpace(subCommand) ||
                subCommand == "مساعدة")
            {
                await Reply(
                    message,
                    GetHelp()
                );

                return;
            }

            // -----------------------------------------------------
            // !بالونات جديد
            // -----------------------------------------------------

            if (subCommand == "جديد")
            {
                if (Game != null && Game.IsRunning)
                {
                    await Reply(
                        message,
                        "🎈 توجد لعبة بالونات قيد التشغيل حالياً."
                    );

                    return;
                }

                Game = new BalloonGame(
                    Client,
                    TargetGroupId
                );

                await Game.Create(message.UserId);

                return;
            }

            // -----------------------------------------------------
            // !بالونات انضم
            // !بالونات انضمام
            // -----------------------------------------------------

            if (subCommand == "انضم" ||
                subCommand == "انضمام")
            {
                if (Game == null)
                {
                    await Reply(
                        message,
                        "❌ ماكو لعبة حالياً.\nاكتب: !بالونات جديد"
                    );

                    return;
                }

                await Game.Join(
                    message.UserId,
                    message
                );

                return;
            }

            // -----------------------------------------------------
            // !بالونات لاعبين
            // -----------------------------------------------------

            if (subCommand == "لاعبين")
            {
                if (Game == null)
                {
                    await Reply(
                        message,
                        "❌ ماكو لعبة حالياً."
                    );

                    return;
                }

                await Game.ShowPlayers(message);

                return;
            }

            // -----------------------------------------------------
            // !بالونات بدء
            // -----------------------------------------------------

            if (subCommand == "بدء")
            {
                if (Game == null)
                {
                    await Reply(
                        message,
                        "❌ ماكو لعبة.\nاكتب: !بالونات جديد"
                    );

                    return;
                }

                await Game.Start(message);

                return;
            }

            // -----------------------------------------------------
            // !بالونات انهاء
            // !بالونات إنهاء
            // -----------------------------------------------------

            if (subCommand == "انهاء" ||
                subCommand == "إنهاء")
            {
                if (Game == null)
                {
                    await Reply(
                        message,
                        "❌ ماكو لعبة حالياً."
                    );

                    return;
                }

                await Game.End(message);

                return;
            }

            await Reply(
                message,
                "❌ الأمر غير معروف.\n\n" +
                GetHelp()
            );
        }

        // =========================================================
        // إرسال رسالة للكروب
        // =========================================================

        public static async Task Reply(
            Message message,
            string text)
        {
            try
            {
                if (message.IsGroup)
                {
                    await Client.GroupMessage(
                        message.GroupId,
                        text
                    );
                }
                else
                {
                    await message.Reply(
                        Client,
                        text
                    );
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "SEND MESSAGE ERROR: " +
                    ex.Message
                );
            }
        }

        public static async Task SendToGroup(string text)
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
                    "GROUP SEND ERROR: " +
                    ex.Message
                );
            }
        }

        // =========================================================
        // مساعدة
        // =========================================================

        private static string GetHelp()
        {
            return
                "🎈🔥 لعبة البالونات 🔥🎈\n\n" +

                "الأوامر:\n" +

                "🎈 !بالونات جديد\n" +
                "إنشاء لعبة جديدة.\n\n" +

                "🎈 !بالونات انضم\n" +
                "الانضمام إلى اللعبة.\n\n" +

                "🎈 !بالونات لاعبين\n" +
                "عرض اللاعبين وعدد بالوناتهم.\n\n" +

                "🎈 !بالونات بدء\n" +
                "بدء اللعبة.\n\n" +

                "🎈 !بالونات انهاء\n" +
                "إنهاء اللعبة.\n\n" +

                "طريقة اللعب:\n" +
                "1️⃣ كل لاعب يبدأ بـ 7 🎈\n" +
                "2️⃣ يظهر رقم كل لاعب.\n" +
                "3️⃣ اللاعب الحالي يرسل رقم الخصم.\n" +
                "4️⃣ بعدها يرسل رقم البالونة.\n" +
                "5️⃣ إذا وصلت بالونات اللاعب إلى صفر يخرج.\n" +
                "🏆 آخر لاعب يبقى هو الفائز.\n\n" +

                "⏱️ لديك 15 ثانية لكل اختيار.";
        }
    }

    // =============================================================
    // لعبة البالونات
    // =============================================================

    public class BalloonGame
    {
        private readonly IWolfClient _client;
        private readonly string _groupId;

        private readonly List<BalloonPlayer> _players =
            new List<BalloonPlayer>();

        private readonly Random _random =
            new Random();

        private CancellationTokenSource? _turnCancellation;

        private int _currentPlayerIndex = 0;

        private bool _waitingForOpponent = false;
        private bool _waitingForBalloon = false;

        private string? _selectedOpponentId;

        public bool IsRunning { get; private set; }

        public BalloonGame(
            IWolfClient client,
            string groupId)
        {
            _client = client;
            _groupId = groupId;
        }

        // =========================================================
        // إنشاء اللعبة
        // =========================================================

        public async Task Create(string creatorId)
        {
            _players.Clear();

            IsRunning = false;

            _currentPlayerIndex = 0;

            _waitingForOpponent = false;
            _waitingForBalloon = false;

            _selectedOpponentId = null;

            await Program.SendToGroup(
                "🎈🔥 تم إنشاء لعبة البالونات! 🔥🎈\n\n" +
                "كل لاعب يبدأ بـ 7 بالونات 🎈\n\n" +
                "للانضمام اكتب:\n" +
                "👉 !بالونات انضم\n\n" +
                "بعد اكتمال اللاعبين اكتب:\n" +
                "👉 !بالونات بدء"
            );
        }

        // =========================================================
        // انضمام لاعب
        // =========================================================

        public async Task Join(
            string userId,
            Message message)
        {
            if (IsRunning)
            {
                await Program.Reply(
                    message,
                    "❌ اللعبة بدأت بالفعل."
                );

                return;
            }

            if (_players.Any(
                p => p.UserId == userId))
            {
                await Program.Reply(
                    message,
                    "⚠️ أنت منضم للعبة بالفعل."
                );

                return;
            }

            var player = new BalloonPlayer
            {
                UserId = userId,
                Number = _players.Count + 1,
                Balloons = 7,
                Alive = true
            };

            _players.Add(player);

            await Program.Reply(
                message,
                "🎈 تم انضمامك للعبة!\n\n" +
                $"رقمك: {player.Number}️⃣\n" +
                "رصيدك: 7 🎈\n\n" +
                "عدد اللاعبين حالياً: " +
                _players.Count
            );
        }

        // =========================================================
        // عرض اللاعبين
        // =========================================================

        public async Task ShowPlayers(Message message)
        {
            if (_players.Count == 0)
            {
                await Program.Reply(
                    message,
                    "🎈 ماكو لاعبين باللعبة حالياً."
                );

                return;
            }

            string result =
                "🎈👥 لاعبي لعبة البالونات 👥🎈\n\n";

            foreach (var player in _players)
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

                if (string.IsNullOrEmpty(balloons))
                    balloons = "💀";

                result +=
                    $"{player.Number}️⃣ لاعب {player.Number} — " +
                    $"{player.Balloons} {balloons}\n";
            }

            await Program.Reply(
                message,
                result
            );
        }

        // =========================================================
        // بدء اللعبة
        // =========================================================

        public async Task Start(Message message)
        {
            if (IsRunning)
            {
                await Program.Reply(
                    message,
                    "⚠️ اللعبة بدأت بالفعل."
                );

                return;
            }

            if (_players.Count < 2)
            {
                await Program.Reply(
                    message,
                    "❌ لازم يكون عندنا لاعبين على الأقل."
                );

                return;
            }

            foreach (var player in _players)
            {
                player.Alive = true;
                player.Balloons = 7;
            }

            _currentPlayerIndex = 0;

            IsRunning = true;

            _waitingForOpponent = false;
            _waitingForBalloon = false;

            _selectedOpponentId = null;

            await Program.SendToGroup(
                "🎈🔥 بدأت لعبة البالونات! 🔥🎈\n\n" +
                "كل لاعب عنده 7 🎈\n" +
                "⏱️ كل اختيار عندك 15 ثانية.\n\n" +
                "🎯 حظاً موفقاً للجميع!"
            );

            await ShowCurrentTurn();
        }

        // =========================================================
        // معالجة الأرقام
        // =========================================================

        public async Task HandleNumber(
            string userId,
            int number,
            Message message)
        {
            if (!IsRunning)
                return;

            BalloonPlayer? current =
                GetCurrentPlayer();

            if (current == null)
                return;

            // فقط اللاعب الحالي يسمح له بالاختيار
            if (current.UserId != userId)
                return;

            // -----------------------------------------------------
            // اختيار الخصم
            // -----------------------------------------------------

            if (!_waitingForOpponent)
            {
                BalloonPlayer? opponent =
                    _players.FirstOrDefault(
                        p =>
                            p.Number == number &&
                            p.Alive &&
                            p.UserId != current.UserId
                    );

                if (opponent == null)
                {
                    await Program.Reply(
                        message,
                        "❌ رقم اللاعب غير صحيح.\n" +
                        "اختار رقم لاعب حي غيرك."
                    );

                    return;
                }

                _selectedOpponentId =
                    opponent.UserId;

                _waitingForOpponent = true;
                _waitingForBalloon = true;

                RestartTimer();

                await Program.SendToGroup(
                    $"🎯 تم اختيار لاعب {opponent.Number}.\n\n" +
                    $"لديه حالياً {opponent.Balloons} 🎈\n" +
                    $"اختار رقم البالونة من 1 إلى {opponent.Balloons}."
                );

                return;
            }

            // -----------------------------------------------------
            // اختيار البالونة
            // -----------------------------------------------------

            if (_waitingForBalloon)
            {
                if (string.IsNullOrWhiteSpace(
                    _selectedOpponentId))
                {
                    await NextTurn();
                    return;
                }

                BalloonPlayer? opponent =
                    _players.FirstOrDefault(
                        p =>
                            p.UserId ==
                            _selectedOpponentId
                    );

                if (opponent == null ||
                    !opponent.Alive)
                {
                    await Program.SendToGroup(
                        "❌ اللاعب لم يعد متاحاً."
                    );

                    await NextTurn();

                    return;
                }

                if (number < 1 ||
                    number > opponent.Balloons)
                {
                    await Program.Reply(
                        message,
                        $"❌ اختار رقم من 1 إلى {opponent.Balloons}."
                    );

                    return;
                }

                StopTimer();

                _waitingForOpponent = false;
                _waitingForBalloon = false;

                _selectedOpponentId = null;

                await ResolveBalloon(
                    current,
                    opponent,
                    number
                );
            }
        }

        // =========================================================
        // نتيجة اختيار البالونة
        // =========================================================

        private async Task ResolveBalloon(
            BalloonPlayer current,
            BalloonPlayer opponent,
            int balloonNumber)
        {
            int roll = _random.Next(1, 101);

            // 15% حظ
            if (roll <= 15)
            {
                await Program.SendToGroup(
                    $"🍀 حظ! اللاعب {current.Number} اختار " +
                    $"البالونة رقم {balloonNumber} " +
                    $"لكنها ما انفجرت!\n\n" +
                    $"🎈 لاعب {opponent.Number} ما زال عنده " +
                    $"{opponent.Balloons} 🎈"
                );

                await NextTurn();

                return;
            }

            // 15% نجاة
            if (roll <= 30)
            {
                await Program.SendToGroup(
                    $"🛡️ نجاة! البالونة رقم {balloonNumber} " +
                    $"بقت مكانها.\n\n" +
                    $"🎈 لاعب {opponent.Number}: " +
                    $"{opponent.Balloons} 🎈"
                );

                await NextTurn();

                return;
            }

            // 10% دور إضافي
            if (roll <= 40)
            {
                opponent.Balloons--;

                await Program.SendToGroup(
                    $"💥 انفجرت البالونة رقم {balloonNumber}!\n\n" +
                    $"🎯 اللاعب {current.Number} حصل على دور إضافي!\n\n" +
                    $"🎈 لاعب {opponent.Number}: " +
                    $"{opponent.Balloons} 🎈"
                );

                if (opponent.Balloons <= 0)
                {
                    opponent.Balloons = 0;
                    opponent.Alive = false;

                    await Program.SendToGroup(
                        $"💀 اللاعب {opponent.Number} خرج من اللعبة!\n"
                    );

                    if (CheckWinner())
                        return;
                }

                // نفس اللاعب يبقى دوره
                await ShowCurrentTurn();

                return;
            }

            // 60% انفجار طبيعي
            opponent.Balloons--;

            if (opponent.Balloons < 0)
                opponent.Balloons = 0;

            await Program.SendToGroup(
                $"💥💥 انفجرت البالونة رقم {balloonNumber}!\n\n" +
                $"🎈 اللاعب {opponent.Number} أصبح عنده " +
                $"{opponent.Balloons} 🎈"
            );

            if (opponent.Balloons == 0)
            {
                opponent.Alive = false;

                await Program.SendToGroup(
                    $"💀 اللاعب {opponent.Number} خرج من اللعبة!"
                );

                if (CheckWinner())
                    return;
            }

            await NextTurn();
        }

        // =========================================================
        // الانتقال للدور التالي
        // =========================================================

        private async Task NextTurn()
        {
            if (!IsRunning)
                return;

            StopTimer();

            _waitingForOpponent = false;
            _waitingForBalloon = false;
            _selectedOpponentId = null;

            if (CheckWinner())
                return;

            int count = _players.Count;

            for (int i = 0; i < count; i++)
            {
                _currentPlayerIndex++;

                if (_currentPlayerIndex >=
                    _players.Count)
                {
                    _currentPlayerIndex = 0;
                }

                if (_players[
                    _currentPlayerIndex
                ].Alive)
                {
                    break;
                }
            }

            await ShowCurrentTurn();
        }

        // =========================================================
        // عرض الدور الحالي
        // =========================================================

        private async Task ShowCurrentTurn()
        {
            if (!IsRunning)
                return;

            BalloonPlayer? current =
                GetCurrentPlayer();

            if (current == null)
                return;

            await Program.SendToGroup(
                "🎯🎈 دور اللاعب " +
                $"{current.Number}️⃣\n\n" +
                "اختار رقم اللاعب اللي تريد تضربه.\n" +
                "مثال:\n" +
                "2\n\n" +
                "⏱️ عندك 15 ثانية."
            );

            _waitingForOpponent = false;
            _waitingForBalloon = false;

            _selectedOpponentId = null;

            RestartTimer();
        }

        // =========================================================
        // مؤقت 15 ثانية
        // =========================================================

        private void RestartTimer()
        {
            StopTimer();

            _turnCancellation =
                new CancellationTokenSource();

            CancellationToken token =
                _turnCancellation.Token;

            _ = TurnTimeout(token);
        }

        private async Task TurnTimeout(
            CancellationToken token)
        {
            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(15),
                    token
                );

                if (token.IsCancellationRequested)
                    return;

                await MessageTimeout();
            }
            catch (TaskCanceledException)
            {
                // طبيعي
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "TIMER ERROR: " +
                    ex.Message
                );
            }
        }

        private async Task MessageTimeout()
        {
            await ProgramLock();

            try
            {
                if (!IsRunning)
                    return;

                BalloonPlayer? current =
                    GetCurrentPlayer();

                if (current == null)
                    return;

                if (_waitingForBalloon)
                {
                    await Program.SendToGroup(
                        $"⏱️ انتهى الوقت على اللاعب " +
                        $"{current.Number}!\n" +
                        "تم تجاوز دوره."
                    );
                }
                else
                {
                    await Program.SendToGroup(
                        $"⏱️ اللاعب {current.Number} " +
                        "ما اختار خصم خلال 15 ثانية.\n" +
                        "تم تجاوز دوره."
                    );
                }

                await NextTurnInternal();
            }
            finally
            {
                ReleaseProgramLock();
            }
        }

        // =========================================================
        // قفل المؤقت حتى لا يتداخل مع الرسائل
        // =========================================================

        private static readonly SemaphoreSlim GameLock =
            new SemaphoreSlim(1, 1);

        private async Task ProgramLock()
        {
            await GameLock.WaitAsync();
        }

        private void ReleaseProgramLock()
        {
            GameLock.Release();
        }

        private async Task NextTurnInternal()
        {
            StopTimer();

            _waitingForOpponent = false;
            _waitingForBalloon = false;
            _selectedOpponentId = null;

            if (CheckWinner())
                return;

            int count = _players.Count;

            for (int i = 0; i < count; i++)
            {
                _currentPlayerIndex++;

                if (_currentPlayerIndex >=
                    _players.Count)
                {
                    _currentPlayerIndex = 0;
                }

                if (_players[
                    _currentPlayerIndex
                ].Alive)
                {
                    break;
                }
            }

            await ShowCurrentTurn();
        }

        // =========================================================
        // إيقاف المؤقت
        // =========================================================

        private void StopTimer()
        {
            try
            {
                _turnCancellation?.Cancel();
                _turnCancellation?.Dispose();
                _turnCancellation = null;
            }
            catch
            {
                // تجاهل
            }
        }

        // =========================================================
        // اللاعب الحالي
        // =========================================================

        private BalloonPlayer? GetCurrentPlayer()
        {
            if (_players.Count == 0)
                return null;

            if (_currentPlayerIndex < 0 ||
                _currentPlayerIndex >= _players.Count)
            {
                _currentPlayerIndex = 0;
            }

            return _players[
                _currentPlayerIndex
            ];
        }

        // =========================================================
        // التحقق من الفائز
        // =========================================================

        private bool CheckWinner()
        {
            if (!IsRunning)
                return true;

            var alive =
                _players
                    .Where(p => p.Alive)
                    .ToList();

            if (alive.Count != 1)
                return false;

            var winner = alive[0];

            IsRunning = false;

            StopTimer();

            _ = Program.SendToGroup(
                "🏆🎉 انتهت لعبة البالونات! 🎉🏆\n\n" +
                $"🥇 الفائز هو اللاعب {winner.Number}️⃣\n\n" +
                "🎈 مبروك للفائز!"
            );

            return true;
        }

        // =========================================================
        // إنهاء اللعبة
        // =========================================================

        public async Task End(Message message)
        {
            if (!IsRunning)
            {
                await Program.Reply(
                    message,
                    "⚠️ اللعبة غير مبدوءة حالياً."
                );

                return;
            }

            IsRunning = false;

            StopTimer();

            _waitingForOpponent = false;
            _waitingForBalloon = false;

            _selectedOpponentId = null;

            await Program.Reply(
                message,
                "🛑 تم إنهاء لعبة البالونات."
            );
        }
    }

    // =============================================================
    // بيانات اللاعب
    // =============================================================

    public class BalloonPlayer
    {
        public string UserId { get; set; } = "";

        public int Number { get; set; }

        public int Balloons { get; set; } = 7;

        public bool Alive { get; set; } = true;
    }
}
