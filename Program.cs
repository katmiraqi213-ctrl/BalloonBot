using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WolfLive.Api;
using WolfLive.Api.Models;

class Program
{
    private static IWolfClient? _client;
    private static BalloonGame? _game;

    static async Task Main()
    {
        Console.WriteLine("🎈 BalloonBot بدأ التشغيل...");

        string email = Environment.GetEnvironmentVariable("WOLF_EMAIL") ?? "";
        string password = Environment.GetEnvironmentVariable("WOLF_PASSWORD") ?? "";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            Console.WriteLine("❌ WOLF_EMAIL أو WOLF_PASSWORD غير موجود.");
            return;
        }

        _client = new WolfClient();

        _client.OnConnected += (_) =>
        {
            Console.WriteLine("✅ تم الاتصال بـ WOLF.");
        };

        _client.Messaging.OnMessage += async (client, message) =>
        {
            try
            {
                await HandleMessage(client, message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ خطأ: " + ex.Message);
            }
        };

        Console.WriteLine("🔐 جاري تسجيل الدخول...");

        bool loginResult = await _client.Login(email, password);

        if (!loginResult)
        {
            Console.WriteLine("❌ فشل تسجيل الدخول.");
            return;
        }

        Console.WriteLine("✅ تم تسجيل الدخول بنجاح.");

        await _client.Connect();

        Console.WriteLine("🚀 BalloonBot يعمل الآن.");

        await Task.Delay(Timeout.Infinite);
    }

    private static async Task HandleMessage(IWolfClient client, Message message)
    {
        string text = message.Text?.Trim() ?? "";
        string userId = message.UserId;
        string groupId = message.GroupId ?? "";

        if (string.IsNullOrWhiteSpace(text))
            return;

        Console.WriteLine(
            $"📩 Group: {groupId} | User: {userId} | Message: {text}"
        );

        // =========================
        // المساعدة
        // =========================

        if (text.Equals("!بالونات", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("!بالونات مساعدة", StringComparison.OrdinalIgnoreCase))
        {
            await Reply(client, message, HelpText());
            return;
        }

        // =========================
        // إنشاء لعبة
        // =========================

        if (text.Equals("!بالونات جديد", StringComparison.OrdinalIgnoreCase))
        {
            if (_game != null)
            {
                await Reply(
                    client,
                    message,
                    "⚠️ توجد لعبة بالونات حالياً.\nأنهوا اللعبة الحالية أولاً باستخدام:\n!بالونات انهاء"
                );

                return;
            }

            string name = await GetNickname(client, userId);

            _game = new BalloonGame
            {
                GroupId = groupId,
                CreatorId = userId
            };

            _game.AddPlayer(userId, name);

            await Reply(
                client,
                message,
                "🎈 تم إنشاء لعبة البالونات!\n\n" +
                "👤 تم تسجيلك تلقائياً.\n\n" +
                "🎯 كل لاعب يبدأ بـ 7 بالونات.\n\n" +
                "للانضمام أرسل:\n" +
                "!بالونات انضم\n\n" +
                "وعند اكتمال اللاعبين أرسل:\n" +
                "!بالونات بدء"
            );

            return;
        }

        // =========================
        // الانضمام
        // =========================

        if (text.Equals("!بالونات انضم", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("!بالونات انضمام", StringComparison.OrdinalIgnoreCase))
        {
            if (_game == null)
            {
                await Reply(
                    client,
                    message,
                    "❌ لا توجد لعبة حالياً.\nأنشئ لعبة باستخدام:\n!بالونات جديد"
                );

                return;
            }

            if (_game.GroupId != groupId)
                return;

            if (_game.Started)
            {
                await Reply(
                    client,
                    message,
                    "❌ اللعبة بدأت بالفعل، لا يمكن الانضمام الآن."
                );

                return;
            }

            if (_game.HasPlayer(userId))
            {
                await Reply(
                    client,
                    message,
                    "⚠️ أنت منضم بالفعل إلى اللعبة."
                );

                return;
            }

            string name = await GetNickname(client, userId);

            _game.AddPlayer(userId, name);

            await Reply(
                client,
                message,
                $"🎈 {name} انضم إلى اللعبة!\n\n" +
                PlayersText(_game)
            );

            return;
        }

        // =========================
        // عرض اللاعبين
        // =========================

        if (text.Equals("!بالونات لاعبين", StringComparison.OrdinalIgnoreCase))
        {
            if (_game == null)
            {
                await Reply(
                    client,
                    message,
                    "❌ لا توجد لعبة حالياً."
                );

                return;
            }

            if (_game.GroupId != groupId)
                return;

            await Reply(
                client,
                message,
                PlayersText(_game)
            );

            return;
        }

        // =========================
        // بدء اللعبة
        // =========================

        if (text.Equals("!بالونات بدء", StringComparison.OrdinalIgnoreCase))
        {
            if (_game == null)
            {
                await Reply(
                    client,
                    message,
                    "❌ لا توجد لعبة.\nأنشئ لعبة باستخدام:\n!بالونات جديد"
                );

                return;
            }

            if (_game.GroupId != groupId)
                return;

            if (_game.Started)
            {
                await Reply(
                    client,
                    message,
                    "⚠️ اللعبة بدأت بالفعل."
                );

                return;
            }

            if (_game.Players.Count < 2)
            {
                await Reply(
                    client,
                    message,
                    "❌ يجب أن يكون هناك لاعبان على الأقل لبدء اللعبة."
                );

                return;
            }

            StartGame(_game);

            string firstName = _game.CurrentPlayer?.Name ?? "غير معروف";

            await Reply(
                client,
                message,
                "🎈🔥 بدأت لعبة البالونات! 🔥🎈\n\n" +
                PlayersText(_game) +
                "\n\n" +
                $"🎯 الدور على: {firstName}\n\n" +
                "👤 اختر رقم اللاعب الذي تريد استهدافه.\n" +
                "مثال: 3"
            );

            return;
        }

        // =========================
        // إنهاء اللعبة
        // =========================

        if (text.Equals("!بالونات انهاء", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("!بالونات إنهاء", StringComparison.OrdinalIgnoreCase))
        {
            if (_game == null)
            {
                await Reply(
                    client,
                    message,
                    "❌ لا توجد لعبة حالياً."
                );

                return;
            }

            if (_game.GroupId != groupId)
                return;

            _game = null;

            await Reply(
                client,
                message,
                "🛑 تم إنهاء لعبة البالونات."
            );

            return;
        }

        // =========================
        // لا توجد لعبة
        // =========================

        if (_game == null)
            return;

        if (_game.GroupId != groupId)
            return;

        if (!_game.Started)
            return;

        // =========================
        // التأكد من اللاعب الحالي
        // =========================

        if (_game.CurrentPlayer == null)
            return;

        if (_game.CurrentPlayer.Id != userId)
            return;

        // =========================
        // اختيار الخصم
        // =========================

        if (_game.WaitingForOpponent)
        {
            if (!int.TryParse(text, out int opponentNumber))
                return;

            BalloonPlayer? opponent = GetPlayerByNumber(_game, opponentNumber);

            if (opponent == null)
            {
                await Reply(
                    client,
                    message,
                    "❌ رقم اللاعب غير صحيح.\nاختر رقماً موجوداً في قائمة اللاعبين."
                );

                return;
            }

            if (opponent.Id == userId)
            {
                await Reply(
                    client,
                    message,
                    "❌ لا يمكنك اختيار نفسك."
                );

                return;
            }

            if (opponent.Eliminated || opponent.Balloons <= 0)
            {
                await Reply(
                    client,
                    message,
                    "❌ هذا اللاعب خرج من اللعبة."
                );

                return;
            }

            _game.SelectedOpponentId = opponent.Id;
            _game.WaitingForOpponent = false;
            _game.WaitingForBalloon = true;

            await Reply(
                client,
                message,
                $"🎯 اخترت: {opponent.Name}\n\n" +
                $"🎈 عدد بالونات {opponent.Name}: {opponent.Balloons}\n\n" +
                "اختر رقم البالون:\n" +
                string.Join(" ", opponent.ActiveBalloons.Select(x => x.ToString()))
            );

            return;
        }

        // =========================
        // اختيار البالون
        // =========================

        if (_game.WaitingForBalloon)
        {
            if (!int.TryParse(text, out int balloonNumber))
                return;

            BalloonPlayer? opponent = _game.Players
                .FirstOrDefault(p => p.Id == _game.SelectedOpponentId);

            if (opponent == null)
            {
                ResetTurnSelection(_game);

                await Reply(
                    client,
                    message,
                    "❌ لم أجد اللاعب المستهدف."
                );

                return;
            }

            if (!opponent.ActiveBalloons.Contains(balloonNumber))
            {
                await Reply(
                    client,
                    message,
                    "❌ رقم البالون غير صحيح.\n\n" +
                    "البالونات المتبقية:\n" +
                    string.Join(
                        " ",
                        opponent.ActiveBalloons.Select(x => x.ToString())
                    )
                );

                return;
            }

            await PlayBalloon(
                client,
                message,
                _game,
                opponent,
                balloonNumber
            );

            return;
        }
    }

    // ============================================================
    // تشغيل حركة البالون
    // ============================================================

    private static async Task PlayBalloon(
        IWolfClient client,
        Message message,
        BalloonGame game,
        BalloonPlayer opponent,
        int balloonNumber)
    {
        Random random = new Random();

        int effect = random.Next(1, 101);

        string result;

        bool extraTurn = false;

        // =========================
        // حظ
        // =========================

        if (effect <= 15)
        {
            result =
                "🍀 حظ سعيد!\n\n" +
                $"🎈 البالون رقم {balloonNumber} لم ينفجر!\n" +
                "😎 نجا البالون هذه المرة.";

            opponent.ActiveBalloons.Remove(balloonNumber);

            // نرجعه لأن تأثير الحظ يعني يبقى
            opponent.ActiveBalloons.Add(balloonNumber);
            opponent.ActiveBalloons.Sort();

            MoveToNextPlayer(game);
        }

        // =========================
        // نجاة
        // =========================

        else if (effect <= 30)
        {
            result =
                "🛡️ نجاة!\n\n" +
                $"🎈 البالون رقم {balloonNumber} بقي سليماً.\n" +
                "🔄 لكن الدور ينتقل للاعب التالي.";

            MoveToNextPlayer(game);
        }

        // =========================
        // دور إضافي
        // =========================

        else if (effect <= 40)
        {
            opponent.ActiveBalloons.Remove(balloonNumber);
            opponent.Balloons--;

            result =
                "💥🎁 انفجار + دور إضافي!\n\n" +
                $"🎈 البالون رقم {balloonNumber} انفجر!\n" +
                $"💔 خسر {opponent.Name} بالوناً.\n" +
                $"🎈 المتبقي لديه: {opponent.Balloons}";

            extraTurn = true;
        }

        // =========================
        // انفجار طبيعي
        // =========================

        else
        {
            opponent.ActiveBalloons.Remove(balloonNumber);
            opponent.Balloons--;

            result =
                "💥 انفجر البالون!\n\n" +
                $"🎈 البالون رقم {balloonNumber} انفجر.\n" +
                $"💔 {opponent.Name} خسر بالوناً.\n" +
                $"🎈 المتبقي لديه: {opponent.Balloons}";

            MoveToNextPlayer(game);
        }

        // =========================
        // الإقصاء
        // =========================

        if (opponent.Balloons <= 0)
        {
            opponent.Balloons = 0;
            opponent.Eliminated = true;
            opponent.ActiveBalloons.Clear();

            result +=
                $"\n\n💀 تم إقصاء {opponent.Name} من اللعبة!";

            RemoveEliminatedFromTurn(game);
        }

        // =========================
        // فحص الفائز
        // =========================

        BalloonPlayer? winner = GetWinner(game);

        if (winner != null)
        {
            await Reply(
                client,
                message,
                result +
                "\n\n" +
                "🏆🎉 انتهت اللعبة! 🎉🏆\n\n" +
                $"👑 الفائز هو: {winner.Name}\n" +
                $"🎈 لديه {winner.Balloons} بالونات."
            );

            game.Started = false;
            game.WaitingForOpponent = false;
            game.WaitingForBalloon = false;

            return;
        }

        // =========================
        // الدور الإضافي
        // =========================

        if (extraTurn && !game.CurrentPlayer!.Eliminated)
        {
            game.WaitingForOpponent = true;
            game.WaitingForBalloon = false;
            game.SelectedOpponentId = "";

            await Reply(
                client,
                message,
                result +
                "\n\n" +
                "🔥 دور إضافي!\n\n" +
                $"🎯 الدور يبقى مع: {game.CurrentPlayer.Name}\n\n" +
                "👤 اختر رقم اللاعب المستهدف."
            );

            return;
        }

        // =========================
        // الدور التالي
        // =========================

        game.WaitingForOpponent = true;
        game.WaitingForBalloon = false;
        game.SelectedOpponentId = "";

        BalloonPlayer? next = game.CurrentPlayer;

        if (next == null)
        {
            game.Started = false;

            await Reply(
                client,
                message,
                result
            );

            return;
        }

        await Reply(
            client,
            message,
            result +
            "\n\n" +
            "📋 حالة اللاعبين:\n" +
            PlayersText(game) +
            "\n\n" +
            $"🎯 الدور الآن على: {next.Name}\n\n" +
            "👤 اختر رقم اللاعب المستهدف."
        );
    }

    // ============================================================
    // بدء اللعبة
    // ============================================================

    private static void StartGame(BalloonGame game)
    {
        game.Started = true;

        game.WaitingForOpponent = true;
        game.WaitingForBalloon = false;
        game.SelectedOpponentId = "";

        foreach (BalloonPlayer player in game.Players)
        {
            player.Balloons = 7;
            player.Eliminated = false;

            player.ActiveBalloons = Enumerable
                .Range(1, 7)
                .ToList();
        }

        game.CurrentPlayerIndex = 0;
    }

    // ============================================================
    // الانتقال للاعب التالي
    // ============================================================

    private static void MoveToNextPlayer(BalloonGame game)
    {
        if (game.Players.Count == 0)
            return;

        int total = game.Players.Count;

        for (int i = 1; i <= total; i++)
        {
            int index =
                (game.CurrentPlayerIndex + i) % total;

            BalloonPlayer player = game.Players[index];

            if (!player.Eliminated && player.Balloons > 0)
            {
                game.CurrentPlayerIndex = index;
                return;
            }
        }
    }

    // ============================================================
    // إزالة اللاعبين المقصيين من الدور
    // ============================================================

    private static void RemoveEliminatedFromTurn(BalloonGame game)
    {
        if (game.CurrentPlayer == null)
            return;

        if (!game.CurrentPlayer.Eliminated)
            return;

        MoveToNextPlayer(game);
    }

    // ============================================================
    // البحث عن الفائز
    // ============================================================

    private static BalloonPlayer? GetWinner(BalloonGame game)
    {
        List<BalloonPlayer> alivePlayers = game.Players
            .Where(p => !p.Eliminated && p.Balloons > 0)
            .ToList();

        if (alivePlayers.Count == 1)
            return alivePlayers[0];

        return null;
    }

    // ============================================================
    // البحث عن لاعب بالرقم
    // ============================================================

    private static BalloonPlayer? GetPlayerByNumber(
        BalloonGame game,
        int number)
    {
        List<BalloonPlayer> alivePlayers = game.Players
            .Where(p => !p.Eliminated && p.Balloons > 0)
            .ToList();

        if (number < 1 || number > alivePlayers.Count)
            return null;

        return alivePlayers[number - 1];
    }

    // ============================================================
    // إعادة حالة الاختيار
    // ============================================================

    private static void ResetTurnSelection(BalloonGame game)
    {
        game.WaitingForOpponent = true;
        game.WaitingForBalloon = false;
        game.SelectedOpponentId = "";
    }

    // ============================================================
    // قائمة اللاعبين
    // ============================================================

    private static string PlayersText(BalloonGame game)
    {
        List<BalloonPlayer> players = game.Players
            .Where(p => !p.Eliminated && p.Balloons > 0)
            .ToList();

        if (players.Count == 0)
            return "👥 لا يوجد لاعبين.";

        string[] numbers =
        {
            "1️⃣",
            "2️⃣",
            "3️⃣",
            "4️⃣",
            "5️⃣",
            "6️⃣",
            "7️⃣",
            "8️⃣",
            "9️⃣",
            "🔟"
        };

        List<string> lines = new List<string>();

        for (int i = 0; i < players.Count; i++)
        {
            string number =
                i < numbers.Length
                    ? numbers[i]
                    : $"{i + 1}.";

            lines.Add(
                $"{number} {players[i].Name} — {players[i].Balloons} 🎈"
            );
        }

        return
            "👥 اللاعبين:\n\n" +
            string.Join("\n", lines);
    }

    // ============================================================
    // المساعدة
    // ============================================================

    private static string HelpText()
    {
        return
            "🎈🔥 لعبة البالونات 🔥🎈\n\n" +

            "📌 الأوامر:\n\n" +

            "!بالونات جديد\n" +
            "🎮 إنشاء لعبة جديدة\n\n" +

            "!بالونات انضم\n" +
            "👤 الانضمام إلى اللعبة\n\n" +

            "!بالونات لاعبين\n" +
            "👥 عرض اللاعبين\n\n" +

            "!بالونات بدء\n" +
            "🚀 بدء اللعبة\n\n" +

            "!بالونات انهاء\n" +
            "🛑 إنهاء اللعبة\n\n" +

            "🎯 طريقة اللعب:\n\n" +

            "كل لاعب يبدأ بـ 7 بالونات 🎈\n\n" +

            "بعد بدء اللعبة يظهر ترتيب اللاعبين.\n\n" +

            "🎯 اللاعب الحالي يرسل رقم الخصم.\n" +
            "مثال:\n" +
            "3\n\n" +

            "بعدها يختار رقم البالون.\n" +
            "مثال:\n" +
            "5\n\n" +

            "💥 إذا انفجر البالون يخسر الخصم بالوناً.\n\n" +

            "🍀 توجد تأثيرات عشوائية.\n\n" +

            "🛡️ نجاة\n" +
            "🔄 دور إضافي\n" +
            "💥 انفجار\n" +
            "🍀 حظ\n\n" +

            "💀 عندما يصل اللاعب إلى 0 بالونات يتم إقصاؤه.\n\n" +

            "🏆 آخر لاعب يبقى هو الفائز!";
    }

    // ============================================================
    // الحصول على اسم اللاعب
    // ============================================================

    private static async Task<string> GetNickname(
        IWolfClient client,
        string userId)
    {
        try
        {
            var user = await client.GetUser(userId);

            if (user != null &&
                !string.IsNullOrWhiteSpace(user.Nickname))
            {
                return user.Nickname;
            }
        }
        catch
        {
        }

        return userId;
    }

    // ============================================================
    // إرسال الرد
    // ============================================================

    private static async Task Reply(
        IWolfClient client,
        Message message,
        string text)
    {
        await client.Reply(message, text);
    }
}

// ================================================================
// اللاعب
// ================================================================

class BalloonPlayer
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public int Balloons { get; set; } = 7;

    public bool Eliminated { get; set; } = false;

    public List<int> ActiveBalloons { get; set; } =
        Enumerable.Range(1, 7).ToList();
}

// ================================================================
// اللعبة
// ================================================================

class BalloonGame
{
    public string GroupId { get; set; } = "";

    public string CreatorId { get; set; } = "";

    public bool Started { get; set; } = false;

    public bool WaitingForOpponent { get; set; } = false;

    public bool WaitingForBalloon { get; set; } = false;

    public string SelectedOpponentId { get; set; } = "";

    public int CurrentPlayerIndex { get; set; } = 0;

    public List<BalloonPlayer> Players { get; set; } =
        new List<BalloonPlayer>();

    public BalloonPlayer? CurrentPlayer
    {
        get
        {
            if (Players.Count == 0)
                return null;

            if (CurrentPlayerIndex < 0 ||
                CurrentPlayerIndex >= Players.Count)
            {
                return null;
            }

            return Players[CurrentPlayerIndex];
        }
    }

    public bool HasPlayer(string id)
    {
        return Players.Any(p => p.Id == id);
    }

    public void AddPlayer(string id, string name)
    {
        if (HasPlayer(id))
            return;

        Players.Add(
            new BalloonPlayer
            {
                Id = id,
                Name = name,
                Balloons = 7,
                Eliminated = false,
                ActiveBalloons =
                    Enumerable.Range(1, 7).ToList()
            }
        );
    }
}
