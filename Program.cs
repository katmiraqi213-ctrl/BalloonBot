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
        public int Goals { get; set; }
        public int Shots { get; set; }
    }

    public class PenaltyGame
    {
        public string GroupId { get; set; } = "";

        public List<PenaltyPlayer> Players { get; set; } =
            new List<PenaltyPlayer>();

        public bool Started { get; set; }

        public int CurrentPlayerIndex { get; set; }

        public bool WaitingForShot { get; set; }

        public CancellationTokenSource? TurnTimeout { get; set; }

        public int TurnVersion { get; set; }

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

    public static class Program
    {
        private static IWolfClient? Client;

        // =====================================================
        // لعبة مستقلة لكل روم
        // =====================================================

        private static readonly Dictionary<string, PenaltyGame> Games =
            new Dictionary<string, PenaltyGame>();

        private static readonly SemaphoreSlim GamesLock =
            new SemaphoreSlim(1, 1);

        private static readonly Random Random =
            new Random();

        private const int MaxPlayers = 10;
        private const int MinPlayers = 2;

        // عدد التسديدات لكل لاعب
        private const int ShotsPerPlayer = 5;

        // الوقت لكل تسديدة
        private const int TurnSeconds = 25;

        // =====================================================
        // MAIN
        // =====================================================

        public static async Task Main(string[] args)
        {
            string email =
                Environment.GetEnvironmentVariable("WOLF_EMAIL") ?? "";

            string password =
                Environment.GetEnvironmentVariable("WOLF_PASSWORD") ?? "";

            Console.WriteLine("======================================");
            Console.WriteLine("       ⚽ PENALTY BOT STARTING");
            Console.WriteLine("======================================");

            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                Console.WriteLine(
                    "❌ WOLF_EMAIL أو WOLF_PASSWORD غير موجود."
                );

                return;
            }

            try
            {
                Client = new WolfClient();

                // =================================================
                // مهم:
                // تسجيل استقبال الرسائل قبل الاتصال
                // =================================================

                Client.On<WolfMessage>(
                    "message send",
                    OnWolfMessage
                );

                Console.WriteLine(
                    "📡 تم تسجيل مستمع الرسائل."
                );

                Console.WriteLine(
                    "🔌 جاري الاتصال بـ Wolf..."
                );

                bool loggedIn =
                    await LoginManually(
                        email,
                        password
                    );

                if (!loggedIn)
                {
                    Console.WriteLine(
                        "❌ فشل تسجيل الدخول."
                    );

                    return;
                }

                Console.WriteLine(
                    "======================================"
                );

                Console.WriteLine(
                    "✅ LOGIN SUCCESS"
                );

                Console.WriteLine(
                    "⚽ PENALTY BOT ONLINE"
                );

                Console.WriteLine(
                    "🌐 MULTI ROOM MODE"
                );

                Console.WriteLine(
                    "⏱️ TURN TIME: 25 SECONDS"
                );

                Console.WriteLine(
                    "📡 WAITING FOR MESSAGES..."
                );

                Console.WriteLine(
                    "======================================"
                );

                await Task.Delay(
                    Timeout.Infinite
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "❌ MAIN ERROR:"
                );

                Console.WriteLine(ex);
            }
        }

        // =====================================================
        // تسجيل الدخول
        // =====================================================

        private static async Task<bool> LoginManually(
            string email,
            string password)
        {
            try
            {
                if (Client == null)
                    return false;

                Console.WriteLine(
                    "🔌 Connecting to WOLF..."
                );

                var welcomeSource =
                    new TaskCompletionSource<bool>(
                        TaskCreationOptions
                            .RunContinuationsAsynchronously
                    );

                Client.On<object>(
                    "welcome",
                    _ =>
                    {
                        Console.WriteLine(
                            "📩 WELCOME RECEIVED"
                        );

                        welcomeSource.TrySetResult(true);
                    }
                );

                await Client.Connect();

                Console.WriteLine(
                    "✅ Connected to WOLF server."
                );

                Console.WriteLine(
                    "⏳ Waiting for WOLF welcome..."
                );

                var completed =
                    await Task.WhenAny(
                        welcomeSource.Task,
                        Task.Delay(
                            TimeSpan.FromSeconds(15)
                        )
                    );

                if (completed != welcomeSource.Task)
                {
                    Console.WriteLine(
                        "❌ WOLF welcome timeout."
                    );

                    return false;
                }

                Console.WriteLine(
                    "🔐 Sending login..."
                );

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
                        "❌ LOGIN RESPONSE IS NULL."
                    );

                    return false;
                }

                Client.Profiling.Profile = user;

                Console.WriteLine(
                    "✅ Login successful."
                );

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "❌ LOGIN ERROR:"
                );

                Console.WriteLine(ex);

                return false;
            }
        }

        // =====================================================
        // استقبال رسائل ولف
        // =====================================================

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
                    "--------------------------------------"
                );

                Console.WriteLine(
                    "📩 MESSAGE RECEIVED"
                );

                Console.WriteLine(
                    $"🏠 GroupId : {message.GroupId}"
                );

                Console.WriteLine(
                    $"👤 UserId  : {message.UserId}"
                );

                Console.WriteLine(
                    $"💬 Content : {message.Content}"
                );

                Console.WriteLine(
                    $"👥 IsGroup : {message.IsGroup}"
                );

                Console.WriteLine(
                    "--------------------------------------"
                );

                // =================================================
                // نعتمد على GroupId
                //
                // إذا GroupId موجود = رسالة من روم
                // وهذا أكثر أماناً من الاعتماد فقط على IsGroup
                // =================================================

                if (string.IsNullOrWhiteSpace(
                    message.GroupId))
                {
                    return;
                }

                await HandleMessage(
                    message
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "❌ MESSAGE ERROR:"
                );

                Console.WriteLine(ex);
            }
        }

        // =====================================================
        // معالجة الأوامر
        // =====================================================

        private static async Task HandleMessage(
            Message message)
        {
            if (Client == null)
                return;

            string content =
                (message.Content ?? "")
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();

            if (string.IsNullOrWhiteSpace(content))
                return;

            string[] parts =
                content.Split(
                    new[]
                    {
                        ' ',
                        '\t'
                    },
                    StringSplitOptions.RemoveEmptyEntries
                );

            if (parts.Length == 0)
                return;

            // =================================================
            // يجب أن يبدأ الأمر بـ !جزاء
            // =================================================

            if (!parts[0].Equals(
                    "!جزاء",
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // =================================================
            // !جزاء
            // =================================================

            if (parts.Length == 1)
            {
                await NewGame(message);
                return;
            }

            string command =
                parts[1].Trim();

            // =================================================
            // مساعدة
            // =================================================

            if (command.Equals("مساعدة") ||
                command.Equals(
                    "help",
                    StringComparison.OrdinalIgnoreCase))
            {
                await Send(
                    message,
                    "⚽🔥 لعبة ضربات الجزاء 🔥⚽\n\n" +

                    "🎮 الأوامر:\n" +
                    "!جزاء — إنشاء لعبة\n" +
                    "!جزاء انضم — الانضمام\n" +
                    "!جزاء لاعبين — عرض اللاعبين\n" +
                    "!جزاء بدء — بدء اللعبة\n" +
                    "!جزاء حالة — حالة اللعبة\n" +
                    "!جزاء انهاء — إنهاء اللعبة\n" +

                    "\n🎯 أثناء دورك:\n" +
                    "1️⃣ يمين\n" +
                    "2️⃣ وسط\n" +
                    "3️⃣ يسار\n\n" +

                    "🧤 الحارس يختار مكاناً عشوائياً.\n" +
                    "إذا نفس المكان = تصدي.\n" +
                    "إذا مكان مختلف = هدف.\n\n" +

                    "⏱️ وقت التسديدة: 25 ثانية\n" +
                    "🎯 لكل لاعب: 5 تسديدات\n" +
                    "👥 من 2 إلى 10 لاعبين"
                );

                return;
            }

            // =================================================
            // انضمام
            // =================================================

            if (command.Equals("انضم") ||
                command.Equals("انضمام"))
            {
                await JoinGame(message);
                return;
            }

            // =================================================
            // اللاعبين
            // =================================================

            if (command.Equals("لاعبين"))
            {
                await ShowPlayers(message);
                return;
            }

            // =================================================
            // بدء
            // =================================================

            if (command.Equals("بدء"))
            {
                await StartGame(message);
                return;
            }

            // =================================================
            // حالة
            // =================================================

            if (command.Equals("حالة"))
            {
                await ShowStatus(message);
                return;
            }

            // =================================================
            // إنهاء
            // =================================================

            if (command.Equals("انهاء") ||
                command.Equals("إنهاء"))
            {
                await EndGame(message);
                return;
            }

            // =================================================
            // تسديدة
            // =================================================

            if (TryParseChoice(
                    command,
                    out int choice))
            {
                await ProcessShot(
                    message,
                    choice
                );

                return;
            }
        }

        // =====================================================
        // إنشاء لعبة
        // =====================================================

        private static async Task NewGame(
            Message message)
        {
            string groupId =
                message.GroupId ?? "";

            if (string.IsNullOrWhiteSpace(groupId))
                return;

            await GamesLock.WaitAsync();

            try
            {
                if (Games.ContainsKey(groupId))
                {
                    await Send(
                        message,
                        "⚠️ توجد لعبة جزاء بالفعل بهذا الروم."
                    );

                    return;
                }

                var game =
                    new PenaltyGame
                    {
                        GroupId = groupId,
                        Started = false,
                        CurrentPlayerIndex = 0,
                        WaitingForShot = false,
                        TurnVersion = 0
                    };

                Games[groupId] = game;

                await Send(
                    message,

                    "⚽🔥 تم إنشاء لعبة ضربات الجزاء! 🔥⚽\n\n" +

                    "👥 الحد الأدنى: 2 لاعبين\n" +
                    "👥 الحد الأقصى: 10 لاعبين\n" +
                    "🎯 لكل لاعب: 5 تسديدات\n" +
                    "⏱️ وقت التسديدة: 25 ثانية\n\n" +

                    "👇 للانضمام:\n" +
                    "!جزاء انضم\n\n" +

                    "وعند اكتمال اللاعبين:\n" +
                    "!جزاء بدء"
                );
            }
            finally
            {
                GamesLock.Release();
            }
        }

        // =====================================================
        // الانضمام
        // =====================================================

        private static async Task JoinGame(
            Message message)
        {
            string groupId =
                message.GroupId ?? "";

            string userId =
                message.UserId ?? "";

            if (string.IsNullOrWhiteSpace(groupId) ||
                string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            await GamesLock.WaitAsync();

            try
            {
                if (!Games.TryGetValue(
                        groupId,
                        out PenaltyGame? game))
                {
                    await Send(
                        message,
                        "❌ لا توجد لعبة بهذا الروم.\n\n" +
                        "اكتب !جزاء لإنشاء لعبة."
                    );

                    return;
                }

                if (game.Started)
                {
                    await Send(
                        message,
                        "❌ اللعبة بدأت بالفعل."
                    );

                    return;
                }

                var existing =
                    game.Players.FirstOrDefault(
                        p => p.UserId == userId
                    );

                if (existing != null)
                {
                    await Send(
                        message,
                        $"⚠️ أنت مشترك بالفعل.\n" +
                        $"👤 رقمك: اللاعب {existing.Number}"
                    );

                    return;
                }

                if (game.Players.Count >= MaxPlayers)
                {
                    await Send(
                        message,
                        "❌ اللعبة مكتملة.\n" +
                        "الحد الأقصى 10 لاعبين."
                    );

                    return;
                }

                int number =
                    game.Players.Count + 1;

                var player =
                    new PenaltyPlayer
                    {
                        UserId = userId,
                        Number = number,
                        Goals = 0,
                        Shots = 0
                    };

                game.Players.Add(player);

                await Send(
                    message,

                    "✅ تم انضمامك للعبة! ⚽\n\n" +
                    $"👤 رقمك: اللاعب {number}\n" +
                    $"👥 اللاعبين: {game.Players.Count}/{MaxPlayers}\n\n" +
                    "عند الجاهزية اكتب:\n" +
                    "!جزاء بدء"
                );
            }
            finally
            {
                GamesLock.Release();
            }
        }

        // =====================================================
        // عرض اللاعبين
        // =====================================================

        private static async Task ShowPlayers(
            Message message)
        {
            string groupId =
                message.GroupId ?? "";

            if (string.IsNullOrWhiteSpace(groupId))
                return;

            await GamesLock.WaitAsync();

            try
            {
                if (!Games.TryGetValue(
                        groupId,
                        out PenaltyGame? game))
                {
                    await Send(
                        message,
                        "❌ لا توجد لعبة."
                    );

                    return;
                }

                if (game.Players.Count == 0)
                {
                    await Send(
                        message,
                        "👥 لا يوجد لاعبين."
                    );

                    return;
                }

                string text =
                    "⚽🔥 لاعبو لعبة الجزاء 🔥⚽\n\n";

                foreach (var player in game.Players)
                {
                    text +=
                        $"👤 اللاعب {player.Number}\n" +
                        $"⚽ الأهداف: {player.Goals}\n" +
                        $"🎯 التسديدات: {player.Shots}/{ShotsPerPlayer}\n\n";
                }

                await Send(
                    message,
                    text
                );
            }
            finally
            {
                GamesLock.Release();
            }
        }

        // =====================================================
        // بدء اللعبة
        // =====================================================

        private static async Task StartGame(
            Message message)
        {
            string groupId =
                message.GroupId ?? "";

            if (string.IsNullOrWhiteSpace(groupId))
                return;

            await GamesLock.WaitAsync();

            try
            {
                if (!Games.TryGetValue(
                        groupId,
                        out PenaltyGame? game))
                {
                    await Send(
                        message,
                        "❌ لا توجد لعبة."
                    );

                    return;
                }

                if (game.Started)
                {
                    await Send(
                        message,
                        "⚠️ اللعبة بدأت بالفعل."
                    );

                    return;
                }

                if (game.Players.Count < MinPlayers)
                {
                    await Send(
                        message,
                        "❌ يجب وجود لاعبين على الأقل."
                    );

                    return;
                }

                game.Started = true;
                game.CurrentPlayerIndex = 0;
                game.WaitingForShot = true;
                game.TurnVersion++;

                var current =
                    game.CurrentPlayer;

                await SendToGroup(
                    groupId,

                    "⚽🔥 بدأت لعبة ضربات الجزاء! 🔥⚽\n\n" +

                    $"👥 عدد اللاعبين: {game.Players.Count}\n" +
                    "🎯 لكل لاعب 5 تسديدات\n" +
                    "⏱️ وقت كل تسديدة 25 ثانية\n\n" +

                    $"🔥 الدور الآن على:\n" +
                    $"👤 اللاعب {current?.Number}\n\n" +

                    "أرسل:\n" +
                    "1️⃣ يمين\n" +
                    "2️⃣ وسط\n" +
                    "3️⃣ يسار"
                );

                StartShotTimeout(game);
            }
            finally
            {
                GamesLock.Release();
            }
        }

        // =====================================================
        // معالجة التسديدة
        // =====================================================

        private static async Task ProcessShot(
            Message message,
            int choice)
        {
            string groupId =
                message.GroupId ?? "";

            string userId =
                message.UserId ?? "";

            if (string.IsNullOrWhiteSpace(groupId) ||
                string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            await GamesLock.WaitAsync();

            try
            {
                if (!Games.TryGetValue(
                        groupId,
                        out PenaltyGame? game))
                {
                    return;
                }

                if (!game.Started ||
                    !game.WaitingForShot)
                {
                    return;
                }

                var player =
                    game.CurrentPlayer;

                if (player == null)
                    return;

                if (player.UserId != userId)
                {
                    await Send(
                        message,
                        $"⛔ مو دورك!\n" +
                        $"🔥 الدور على اللاعب {player.Number}"
                    );

                    return;
                }

                if (choice < 1 ||
                    choice > 3)
                {
                    await Send(
                        message,
                        "❌ اختر 1 أو 2 أو 3."
                    );

                    return;
                }

                // إلغاء مؤقت الـ25 ثانية
                CancelTurnTimeout(game);

                game.WaitingForShot = false;
                game.TurnVersion++;

                player.Shots++;

                int goalkeeper =
                    Random.Next(1, 4);

                bool goal =
                    choice != goalkeeper;

                string shotName =
                    GetDirectionName(choice);

                string goalkeeperName =
                    GetDirectionName(goalkeeper);

                if (goal)
                {
                    player.Goals++;

                    await SendToGroup(
                        groupId,

                        "⚽🔥 هــــــــدف!!! 🔥⚽\n\n" +

                        $"👤 اللاعب {player.Number}\n" +
                        $"🎯 سدد: {shotName}\n" +
                        $"🧤 الحارس: {goalkeeperName}\n\n" +

                        $"⚽ الأهداف: {player.Goals}\n" +
                        $"🎯 التسديدات: {player.Shots}/{ShotsPerPlayer}"
                    );
                }
                else
                {
                    await SendToGroup(
                        groupId,

                        "🧤❌ تـــــم التصدي!\n\n" +

                        $"👤 اللاعب {player.Number}\n" +
                        $"🎯 سدد: {shotName}\n" +
                        $"🧤 الحارس: {goalkeeperName}\n\n" +

                        $"⚽ الأهداف: {player.Goals}\n" +
                        $"🎯 التسديدات: {player.Shots}/{ShotsPerPlayer}"
                    );
                }

                // هل كل اللاعبين خلصوا؟
                if (AllPlayersFinished(game))
                {
                    await FinishGame(game);
                    return;
                }

                // الانتقال للاعب التالي
                MoveToNextPlayer(game);

                var nextPlayer =
                    game.CurrentPlayer;

                if (nextPlayer == null)
                {
                    await FinishGame(game);
                    return;
                }

                game.WaitingForShot = true;
                game.TurnVersion++;

                await SendToGroup(
                    groupId,

                    "➡️ الدور التالي\n\n" +

                    $"👤 اللاعب {nextPlayer.Number}\n" +
                    $"🎯 التسديدة {nextPlayer.Shots + 1}/{ShotsPerPlayer}\n" +
                    $"⏱️ أمامك {TurnSeconds} ثانية\n\n" +

                    "1️⃣ يمين\n" +
                    "2️⃣ وسط\n" +
                    "3️⃣ يسار"
                );

                StartShotTimeout(game);
            }
            finally
            {
                GamesLock.Release();
            }
        }

        // =====================================================
        // مؤقت 25 ثانية
        // =====================================================

        private static void StartShotTimeout(
            PenaltyGame game)
        {
            CancelTurnTimeout(game);

            var cts =
                new CancellationTokenSource();

            game.TurnTimeout = cts;

            int version =
                game.TurnVersion;

            string groupId =
                game.GroupId;

            int playerIndex =
                game.CurrentPlayerIndex;

            var player =
                game.CurrentPlayer;

            if (player == null)
                return;

            string playerId =
                player.UserId;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(
                            TurnSeconds
                        ),
                        cts.Token
                    );

                    await GamesLock.WaitAsync();

                    try
                    {
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

                        if (currentGame.CurrentPlayerIndex !=
                            playerIndex)
                        {
                            return;
                        }

                        var currentPlayer =
                            currentGame.CurrentPlayer;

                        if (currentPlayer == null)
                            return;

                        if (currentPlayer.UserId != playerId)
                            return;

                        // =========================================
                        // انتهت الـ25 ثانية
                        // =========================================

                        currentGame.WaitingForShot = false;
                        currentGame.TurnVersion++;

                        await SendToGroup(
                            groupId,

                            "⏰ انتهى الوقت!\n\n" +

                            $"👤 اللاعب {currentPlayer.Number}\n" +

                            $"🚪 خرج من لعبة الجزاء بسبب عدم " +
                            $"التسديد خلال {TurnSeconds} ثانية.\n\n" +

                            "⚠️ تم إخراجه من اللعبة فقط.\n" +
                            "🏠 لم يتم طرده من روم ولف."
                        );

                        // =========================================
                        // حذف اللاعب من لعبة الجزاء فقط
                        // لا يوجد Kick للروم
                        // =========================================

                        int removedIndex =
                            currentGame.CurrentPlayerIndex;

                        currentGame.Players.RemoveAt(
                            removedIndex
                        );

                        // =========================================
                        // إذا لم يبقَ أي لاعب
                        // =========================================

                        if (currentGame.Players.Count == 0)
                        {
                            await FinishGame(
                                currentGame
                            );

                            return;
                        }

                        // =========================================
                        // ضبط مكان اللاعب الحالي بعد الحذف
                        // =========================================

                        if (removedIndex >=
                            currentGame.Players.Count)
                        {
                            currentGame.CurrentPlayerIndex = 0;
                        }
                        else
                        {
                            // نخلي المؤشر قبل اللاعب التالي
                            // حتى MoveToNextPlayer يختاره
                            currentGame.CurrentPlayerIndex =
                                removedIndex == 0
                                    ? currentGame.Players.Count - 1
                                    : removedIndex - 1;
                        }

                        // =========================================
                        // هل انتهت اللعبة؟
                        // =========================================

                        if (AllPlayersFinished(
                                currentGame))
                        {
                            await FinishGame(
                                currentGame
                            );

                            return;
                        }

                        // =========================================
                        // اللاعب التالي
                        // =========================================

                        MoveToNextPlayer(
                            currentGame
                        );

                        var nextPlayer =
                            currentGame.CurrentPlayer;

                        if (nextPlayer == null)
                        {
                            await FinishGame(
                                currentGame
                            );

                            return;
                        }

                        currentGame.WaitingForShot = true;
                        currentGame.TurnVersion++;

                        await SendToGroup(
                            groupId,

                            "➡️ انتقل الدور للاعب التالي\n\n" +

                            $"👤 اللاعب {nextPlayer.Number}\n" +
                            $"🎯 التسديدة {nextPlayer.Shots + 1}/{ShotsPerPlayer}\n" +
                            $"⏱️ أمامك {TurnSeconds} ثانية\n\n" +

                            "1️⃣ يمين\n" +
                            "2️⃣ وسط\n" +
                            "3️⃣ يسار"
                        );

                        StartShotTimeout(
                            currentGame
                        );
                    }
                    finally
                    {
                        GamesLock.Release();
                    }
                }
                catch (OperationCanceledException)
                {
                    // المؤقت ألغي بشكل طبيعي
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        "❌ TIMEOUT ERROR:"
                    );

                    Console.WriteLine(ex);
                }
            });
        }

        // =====================================================
        // الانتقال للاعب التالي
        // =====================================================

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

                if (game.Players[index].Shots <
                    ShotsPerPlayer)
                {
                    game.CurrentPlayerIndex =
                        index;

                    return;
                }
            }
        }

        // =====================================================
        // هل جميع اللاعبين خلصوا؟
        // =====================================================

        private static bool AllPlayersFinished(
            PenaltyGame game)
        {
            if (game.Players.Count == 0)
                return true;

            return game.Players.All(
                p => p.Shots >= ShotsPerPlayer
            );
        }

        // =====================================================
        // إنهاء اللعبة
        // =====================================================

        private static async Task FinishGame(
            PenaltyGame game)
        {
            CancelTurnTimeout(game);

            game.Started = false;
            game.WaitingForShot = false;
            game.TurnVersion++;

            if (game.Players.Count == 0)
            {
                await SendToGroup(
                    game.GroupId,
                    "🏁 انتهت اللعبة.\n\n" +
                    "❌ لم يبقَ أي لاعب."
                );

                Games.Remove(
                    game.GroupId
                );

                return;
            }

            var ranking =
                game.Players
                    .OrderByDescending(
                        p => p.Goals
                    )
                    .ThenByDescending(
                        p => p.Shots
                    )
                    .ToList();

            string result =
                "";

            for (int i = 0;
                 i < ranking.Count;
                 i++)
            {
                var p =
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

                result +=
                    $"{medal} اللاعب {p.Number}\n" +
                    $"⚽ الأهداف: {p.Goals}\n" +
                    $"🎯 التسديدات: {p.Shots}/{ShotsPerPlayer}\n\n";
            }

            int highest =
                ranking.First().Goals;

            var winners =
                ranking
                    .Where(
                        p => p.Goals == highest
                    )
                    .ToList();

            string winnerText;

            if (winners.Count == 1)
            {
                winnerText =
                    "👑🏆 الفائز:\n" +
                    $"اللاعب {winners[0].Number}\n" +
                    $"⚽ {winners[0].Goals} أهداف 🔥";
            }
            else
            {
                winnerText =
                    "🤝 تعادل!\n\n" +
                    "🏆 الفائزون:\n";

                foreach (var winner in winners)
                {
                    winnerText +=
                        $"👑 اللاعب {winner.Number} — " +
                        $"{winner.Goals} أهداف\n";
                }
            }

            await SendToGroup(
                game.GroupId,

                "🏆🔥 انتهت لعبة ضربات الجزاء! 🔥🏆\n\n" +
                "📊 النتائج:\n\n" +
                result +
                winnerText
            );

            Games.Remove(
                game.GroupId
            );
        }

        // =====================================================
        // حالة اللعبة
        // =====================================================

        private static async Task ShowStatus(
            Message message)
        {
            string groupId =
                message.GroupId ?? "";

            if (string.IsNullOrWhiteSpace(groupId))
                return;

            await GamesLock.WaitAsync();

            try
            {
                if (!Games.TryGetValue(
                        groupId,
                        out PenaltyGame? game))
                {
                    await Send(
                        message,
                        "❌ لا توجد لعبة."
                    );

                    return;
                }

                if (!game.Started)
                {
                    await Send(
                        message,

                        "⚽ حالة لعبة الجزاء\n\n" +
                        $"👥 اللاعبين: {game.Players.Count}/{MaxPlayers}\n" +
                        "⏳ اللعبة لم تبدأ بعد."
                    );

                    return;
                }

                var current =
                    game.CurrentPlayer;

                if (current == null)
                {
                    await Send(
                        message,
                        "⚠️ لا يوجد لاعب حالي."
                    );

                    return;
                }

                await Send(
                    message,

                    "⚽🔥 حالة لعبة الجزاء 🔥⚽\n\n" +

                    $"👥 اللاعبين المتبقين: {game.Players.Count}\n" +
                    $"👤 الدور على اللاعب: {current.Number}\n" +
                    $"🎯 التسديدة: {current.Shots + 1}/{ShotsPerPlayer}\n" +
                    $"⚽ أهدافه: {current.Goals}\n" +
                    $"⏱️ وقت الدور: {TurnSeconds} ثانية"
                );
            }
            finally
            {
                GamesLock.Release();
            }
        }

        // =====================================================
        // إنهاء يدوي
        // =====================================================

        private static async Task EndGame(
            Message message)
        {
            string groupId =
                message.GroupId ?? "";

            if (string.IsNullOrWhiteSpace(groupId))
                return;

            await GamesLock.WaitAsync();

            try
            {
                if (!Games.TryGetValue(
                        groupId,
                        out PenaltyGame? game))
                {
                    await Send(
                        message,
                        "❌ لا توجد لعبة."
                    );

                    return;
                }

                CancelTurnTimeout(game);

                game.Started = false;
                game.WaitingForShot = false;
                game.TurnVersion++;

                Games.Remove(
                    groupId
                );

                await Send(
                    message,
                    "🛑 تم إنهاء لعبة الجزاء بهذا الروم."
                );
            }
            finally
            {
                GamesLock.Release();
            }
        }

        // =====================================================
        // إلغاء المؤقت
        // =====================================================

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
                // تجاهل الخطأ
            }
        }

        // =====================================================
        // قراءة اختيار اللاعب
        // =====================================================

        private static bool TryParseChoice(
            string text,
            out int choice)
        {
            choice = 0;

            text =
                text
                    .Trim()
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

            if (!int.TryParse(
                    text,
                    out choice))
            {
                return false;
            }

            return choice >= 1 &&
                   choice <= 3;
        }

        // =====================================================
        // أسماء الاتجاهات
        // =====================================================

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

        // =====================================================
        // إرسال رد على الرسالة
        // =====================================================

        private static async Task Send(
            Message message,
            string text)
        {
            try
            {
                if (Client == null)
                    return;

                await message.Reply(
                    Client,
                    text
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "❌ SEND ERROR:"
                );

                Console.WriteLine(ex);
            }
        }

        // =====================================================
        // إرسال رسالة للروم
        // =====================================================

        private static async Task SendToGroup(
            string groupId,
            string text)
        {
            try
            {
                if (Client == null)
                    return;

                if (string.IsNullOrWhiteSpace(
                        groupId))
                {
                    return;
                }

                await Client.GroupMessage(
                    groupId,
                    text
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "❌ GROUP SEND ERROR:"
                );

                Console.WriteLine(ex);
            }
        }
    }
}
