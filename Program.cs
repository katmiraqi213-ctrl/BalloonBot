using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WolfLive.Api;
using WolfLive.Api.Commands;
using WolfLive.Api.Models;

public class Program
{
    // =========================================================
    // إعدادات البوت
    // =========================================================

    public const string TargetGroupId = "82041031";

    public static WolfClient Client { get; private set; } = null!;

    public static BalloonGame? Game { get; set; }

    // =========================================================
    // Main
    // =========================================================

    public static async Task Main(string[] args)
    {
        Console.WriteLine("=================================");
        Console.WriteLine("        BalloonBot");
        Console.WriteLine("=================================");

        string email = Environment.GetEnvironmentVariable("WOLF_EMAIL") ?? "";
        string password = Environment.GetEnvironmentVariable("WOLF_PASSWORD") ?? "";

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
        {
            Console.WriteLine("خطأ: WOLF_EMAIL أو WOLF_PASSWORD غير موجود.");
            return;
        }

        Client = new WolfClient()
            .SetupCommands()
            .WithCommandSet(c =>
            {
                c.AddCommands<BalloonCommands>()
                    .WithPrefix("!");
            })
            .WithSerilog()
            .Done();

        Client.OnConnected += (_) =>
        {
            Console.WriteLine("Connected to wolf.live!");
        };

        // استقبال الرسائل العادية حتى نقدر نقرأ أرقام اللاعبين والبالونات
        Client.Messaging.OnMessage += OnMessage;

        Console.WriteLine("جاري تسجيل الدخول...");

        var result = await Client.Login(email, password);

        Console.WriteLine("Login result: " + result);

        // =====================================================
        // الاشتراك بالروم المطلوبة فقط
        // =====================================================

        try
        {
            bool subscribed =
                await Client.Messaging.GroupMessageSubscribe(TargetGroupId);

            Console.WriteLine(
                "Group subscription: " + subscribed
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "خطأ أثناء الاشتراك بالروم: " + ex.Message
            );
        }

        Console.WriteLine(
            "البوت يعمل الآن في الروم: " + TargetGroupId
        );

        // إبقاء البرنامج شغال
        await Task.Delay(Timeout.Infinite);
    }

    // =========================================================
    // استقبال الرسائل والأرقام
    // =========================================================

    private static async Task OnMessage(
        IWolfClient client,
        Message message)
    {
        try
        {
            // نتعامل فقط مع الرسائل داخل الروم المطلوبة
            if (!message.IsGroup)
                return;

            if (message.GroupId != TargetGroupId)
                return;

            BalloonGame? game = Game;

            if (game == null)
                return;

            if (!game.Started)
                return;

            string text = (message.Content ?? "").Trim();

            if (string.IsNullOrWhiteSpace(text))
                return;

            // -------------------------------------------------
            // لازم تكون الرسالة رقم فقط
            // -------------------------------------------------

            if (!int.TryParse(text, out int number))
                return;

            // -------------------------------------------------
            // تأكد أن صاحب الرسالة هو اللاعب الحالي
            // -------------------------------------------------

            BalloonPlayer? current = game.GetCurrentPlayer();

            if (current == null)
                return;

            if (message.UserId != current.UserId)
                return;

            // -------------------------------------------------
            // اختيار الخصم
            // -------------------------------------------------

            if (game.WaitingForOpponent)
            {
                await HandleOpponentNumber(message, game, number);
                return;
            }

            // -------------------------------------------------
            // اختيار البالونة
            // -------------------------------------------------

            if (game.WaitingForBalloon)
            {
                await HandleBalloonNumber(message, game, number);
                return;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "خطأ في استقبال الرسالة: " + ex
            );
        }
    }

    // =========================================================
    // اختيار الخصم
    // =========================================================

    private static async Task HandleOpponentNumber(
        Message message,
        BalloonGame game,
        int number)
    {
        BalloonPlayer? current = game.GetCurrentPlayer();

        if (current == null)
            return;

        if (number < 1 || number > game.Players.Count)
        {
            await message.Reply(
                Client,
                "❌ رقم اللاعب غير صحيح."
            );

            return;
        }

        BalloonPlayer opponent = game.Players[number - 1];

        // لا يختار نفسه
        if (opponent.UserId == current.UserId)
        {
            await message.Reply(
                Client,
                "❌ ما تگدر تختار نفسك 😄"
            );

            return;
        }

        // اللاعب خارج اللعبة
        if (!opponent.Alive)
        {
            await message.Reply(
                Client,
                "❌ هذا اللاعب طالع من اللعبة."
            );

            return;
        }

        // حفظ الخصم
        game.SelectedOpponent = opponent;

        // إلغاء مؤقت الخصم
        game.CancelTimer();

        game.WaitingForOpponent = false;
        game.WaitingForBalloon = true;

        game.StartTimer();

        await message.Reply(
            Client,
            $"🎯 اخترت: لاعب {opponent.Number}\n\n" +
            $"🎈 عنده {opponent.Balloons} بالونات.\n" +
            $"اختار رقم البالونة من 1 إلى {opponent.Balloons}."
        );
    }

    // =========================================================
    // اختيار البالونة
    // =========================================================

    private static async Task HandleBalloonNumber(
        Message message,
        BalloonGame game,
        int number)
    {
        BalloonPlayer? current = game.GetCurrentPlayer();
        BalloonPlayer? opponent = game.SelectedOpponent;

        if (current == null || opponent == null)
            return;

        if (!opponent.Alive)
        {
            await message.Reply(
                Client,
                "❌ هذا اللاعب لم يعد موجوداً باللعبة."
            );

            game.NextTurn();
            return;
        }

        if (number < 1 || number > opponent.Balloons)
        {
            await message.Reply(
                Client,
                $"❌ رقم البالونة غير صحيح.\n" +
                $"اختار من 1 إلى {opponent.Balloons}."
            );

            return;
        }

        game.CancelTimer();

        game.WaitingForBalloon = false;

        // =====================================================
        // النتيجة العشوائية
        // =====================================================

        int chance = Random.Shared.Next(1, 101);

        // 15% حظ
        if (chance <= 15)
        {
            await message.Reply(
                Client,
                $"🍀 حظ!\n\n" +
                $"البالونة رقم {number} نجت وما انفجرت 🎈\n\n" +
                $"الدور ينتقل للاعب التالي."
            );

            game.NextTurn();
            return;
        }

        // 15% نجاة
        if (chance <= 30)
        {
            await message.Reply(
                Client,
                $"🛡️ نجاة!\n\n" +
                $"البالونة رقم {number} بقيت سليمة 🎈\n\n" +
                $"الدور ينتقل للاعب التالي."
            );

            game.NextTurn();
            return;
        }

        // 10% دور إضافي
        if (chance <= 40)
        {
            opponent.Balloons--;

            if (opponent.Balloons <= 0)
            {
                opponent.Balloons = 0;
                opponent.Alive = false;

                await message.Reply(
                    Client,
                    $"🔄 دور إضافي!\n" +
                    $"💥 البالونة رقم {number} انفجرت!\n\n" +
                    $"☠️ لاعب {opponent.Number} خرج من اللعبة.\n\n" +
                    $"لكن عندك دور إضافي!"
                );

                if (game.CheckWinner(out BalloonPlayer? winner))
                {
                    await FinishGame(message, game, winner!);
                    return;
                }

                game.SelectedOpponent = null;
                game.WaitingForOpponent = true;
                game.WaitingForBalloon = false;

                game.StartTimer();

                await message.Reply(
                    Client,
                    $"🔥 دور إضافي!\n\n" +
                    $"🎯 اختار لاعب ثاني.\n\n" +
                    game.GetPlayersText()
                );

                return;
            }

            await message.Reply(
                Client,
                $"🔄 دور إضافي!\n\n" +
                $"💥 البالونة رقم {number} انفجرت!\n" +
                $"🎈 باقي عند لاعب {opponent.Number}: {opponent.Balloons}\n\n" +
                $"🔥 عندك دور إضافي!"
            );

            game.SelectedOpponent = null;
            game.WaitingForOpponent = true;
            game.WaitingForBalloon = false;

            game.StartTimer();

            await message.Reply(
                Client,
                $"🎯 اختار لاعب ثاني.\n\n" +
                game.GetPlayersText()
            );

            return;
        }

        // 60% انفجار طبيعي
        opponent.Balloons--;

        if (opponent.Balloons < 0)
            opponent.Balloons = 0;

        if (opponent.Balloons == 0)
        {
            opponent.Alive = false;

            await message.Reply(
                Client,
                $"💥 انفجرت البالونة رقم {number}!\n\n" +
                $"☠️ لاعب {opponent.Number} خرج من اللعبة."
            );

            if (game.CheckWinner(out BalloonPlayer? winner))
            {
                await FinishGame(message, game, winner!);
                return;
            }
        }
        else
        {
            await message.Reply(
                Client,
                $"💥 انفجرت البالونة رقم {number}!\n\n" +
                $"🎈 باقي للاعب {opponent.Number}: " +
                $"{opponent.Balloons} بالونات."
            );
        }

        game.NextTurn();

        if (!game.Finished)
        {
            await message.Reply(
                Client,
                game.GetTurnText()
            );
        }
    }

    // =========================================================
    // إنهاء اللعبة
    // =========================================================

    private static async Task FinishGame(
        Message message,
        BalloonGame game,
        BalloonPlayer winner)
    {
        game.Finished = true;
        game.Started = false;
        game.CancelTimer();

        await message.Reply(
            Client,
            $"🏆🎉 انتهت لعبة البالونات! 🎉🏆\n\n" +
            $"🥇 الفائز: لاعب {winner.Number}\n" +
            $"🎈 بالونات الفائز: {winner.Balloons}\n\n" +
            $"مبروك للفائز 👑🔥"
        );
    }
}


// =============================================================
// أوامر البوت
// =============================================================

public class BalloonCommands : WolfContext
{
    [Command("بالونات")]
    public async Task BalloonsCommand(string message = "")
    {
        try
        {
            // فقط الروم المحددة
            if (!Message.IsGroup ||
                Message.GroupId != Program.TargetGroupId)
            {
                return;
            }

            string command = (message ?? "").Trim();

            // -------------------------------------------------
            // !بالونات
            // -------------------------------------------------

            if (string.IsNullOrWhiteSpace(command) ||
                command == "مساعدة")
            {
                await this.Reply(
                    "🎈 لعبة البالونات 🎈\n\n" +

                    "🎮 الأوامر:\n\n" +

                    "🎈 !بالونات جديد\n" +
                    "➜ إنشاء لعبة جديدة\n\n" +

                    "🎈 !بالونات انضم\n" +
                    "➜ الانضمام للعبة\n\n" +

                    "🎈 !بالونات لاعبين\n" +
                    "➜ عرض اللاعبين والبالونات\n\n" +

                    "🎈 !بالونات بدء\n" +
                    "➜ بدء اللعبة\n\n" +

                    "🎈 !بالونات انهاء\n" +
                    "➜ إنهاء اللعبة\n\n" +

                    "📌 كل لاعب يبدأ بـ 7 🎈\n" +
                    "📌 أثناء الدور ارسل رقم اللاعب فقط.\n" +
                    "📌 بعدها ارسل رقم البالونة.\n" +
                    "📌 الوقت لكل اختيار 15 ثانية."
                );

                return;
            }

            // -------------------------------------------------
            // جديد
            // -------------------------------------------------

            if (command == "جديد")
            {
                if (Program.Game != null &&
                    !Program.Game.Finished)
                {
                    await this.Reply(
                        "❌ توجد لعبة حالياً.\n" +
                        "استخدم !بالونات انهاء أولاً."
                    );

                    return;
                }

                Program.Game = new BalloonGame(
                    Program.TargetGroupId
                );

                await this.Reply(
                    "🎈🎉 تم إنشاء لعبة البالونات! 🎉🎈\n\n" +
                    "كل لاعب يبدأ بـ 7 بالونات.\n\n" +
                    "للانضمام اكتب:\n" +
                    "👉 !بالونات انضم"
                );

                return;
            }

            // -------------------------------------------------
            // انضم
            // -------------------------------------------------

            if (command == "انضم" ||
                command == "انضمام")
            {
                BalloonGame? game = Program.Game;

                if (game == null)
                {
                    await this.Reply(
                        "❌ لا توجد لعبة حالياً.\n" +
                        "اكتب !بالونات جديد"
                    );

                    return;
                }

                if (game.Started)
                {
                    await this.Reply(
                        "❌ اللعبة بدأت بالفعل."
                    );

                    return;
                }

                if (game.Finished)
                {
                    await this.Reply(
                        "❌ اللعبة منتهية.\n" +
                        "اكتب !بالونات جديد"
                    );

                    return;
                }

                bool joined = game.AddPlayer(
                    Message.UserId
                );

                if (!joined)
                {
                    if (game.HasPlayer(Message.UserId))
                    {
                        await this.Reply(
                            "⚠️ أنت منضم للعبة مسبقاً."
                        );
                    }
                    else
                    {
                        await this.Reply(
                            "❌ لا يمكن الانضمام للعبة."
                        );
                    }

                    return;
                }

                BalloonPlayer? player =
                    game.GetPlayer(Message.UserId);

                await this.Reply(
                    $"🎉 تم انضمامك للعبة!\n\n" +
                    $"🆔 رقمك: {player!.Number}\n" +
                    $"🎈 بالوناتك: 7\n\n" +
                    game.GetPlayersText()
                );

                return;
            }

            // -------------------------------------------------
            // اللاعبين
            // -------------------------------------------------

            if (command == "لاعبين")
            {
                BalloonGame? game = Program.Game;

                if (game == null)
                {
                    await this.Reply(
                        "❌ لا توجد لعبة حالياً."
                    );

                    return;
                }

                await this.Reply(
                    "🎈 اللاعبين:\n\n" +
                    game.GetPlayersText()
                );

                return;
            }

            // -------------------------------------------------
            // بدء
            // -------------------------------------------------

            if (command == "بدء")
            {
                BalloonGame? game = Program.Game;

                if (game == null)
                {
                    await this.Reply(
                        "❌ لا توجد لعبة.\n" +
                        "اكتب !بالونات جديد"
                    );

                    return;
                }

                if (game.Started)
                {
                    await this.Reply(
                        "⚠️ اللعبة بدأت مسبقاً."
                    );

                    return;
                }

                if (game.Players.Count < 2)
                {
                    await this.Reply(
                        "❌ لازم يكون هناك لاعبين على الأقل."
                    );

                    return;
                }

                game.StartGame();

                await this.Reply(
                    "🔥🎈 بدأت اللعبة! 🎈🔥\n\n" +
                    game.GetPlayersText() +
                    "\n\n" +
                    game.GetTurnText()
                );

                return;
            }

            // -------------------------------------------------
            // انهاء
            // -------------------------------------------------

            if (command == "انهاء" ||
                command == "إنهاء")
            {
                BalloonGame? game = Program.Game;

                if (game == null)
                {
                    await this.Reply(
                        "❌ لا توجد لعبة حالياً."
                    );

                    return;
                }

                game.Finished = true;
                game.Started = false;
                game.CancelTimer();

                Program.Game = null;

                await this.Reply(
                    "🛑 تم إنهاء لعبة البالونات."
                );

                return;
            }

            // -------------------------------------------------
            // أمر غير معروف
            // -------------------------------------------------

            await this.Reply(
                "❌ الأمر غير معروف.\n\n" +
                "اكتب !بالونات مساعدة"
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "خطأ في أمر البالونات: " + ex
            );
        }
    }
}


// =============================================================
// اللاعب
// =============================================================

public class BalloonPlayer
{
    public string UserId { get; }

    public int Number { get; }

    public int Balloons { get; set; }

    public bool Alive { get; set; }

    public BalloonPlayer(
        string userId,
        int number)
    {
        UserId = userId;
        Number = number;
        Balloons = 7;
        Alive = true;
    }
}


// =============================================================
// اللعبة
// =============================================================

public class BalloonGame
{
    public string GroupId { get; }

    public List<BalloonPlayer> Players { get; }

    public bool Started { get; set; }

    public bool Finished { get; set; }

    public bool WaitingForOpponent { get; set; }

    public bool WaitingForBalloon { get; set; }

    public BalloonPlayer? SelectedOpponent { get; set; }

    public int CurrentIndex { get; private set; }

    private CancellationTokenSource? _timerCancellation;

    public BalloonGame(string groupId)
    {
        GroupId = groupId;
        Players = new List<BalloonPlayer>();

        Started = false;
        Finished = false;

        WaitingForOpponent = false;
        WaitingForBalloon = false;

        CurrentIndex = 0;
    }

    // =========================================================
    // إضافة لاعب
    // =========================================================

    public bool AddPlayer(string userId)
    {
        if (Started || Finished)
            return false;

        if (HasPlayer(userId))
            return false;

        int number = Players.Count + 1;

        Players.Add(
            new BalloonPlayer(
                userId,
                number
            )
        );

        return true;
    }

    // =========================================================
    // هل اللاعب موجود؟
    // =========================================================

    public bool HasPlayer(string userId)
    {
        return Players.Any(
            p => p.UserId == userId
        );
    }

    // =========================================================
    // جلب اللاعب
    // =========================================================

    public BalloonPlayer? GetPlayer(string userId)
    {
        return Players.FirstOrDefault(
            p => p.UserId == userId
        );
    }

    // =========================================================
    // بدء اللعبة
    // =========================================================

    public void StartGame()
    {
        if (Players.Count < 2)
            return;

        Started = true;
        Finished = false;

        CurrentIndex = 0;

        SelectedOpponent = null;

        WaitingForOpponent = true;
        WaitingForBalloon = false;

        StartTimer();
    }

    // =========================================================
    // اللاعب الحالي
    // =========================================================

    public BalloonPlayer? GetCurrentPlayer()
    {
        if (Players.Count == 0)
            return null;

        if (CurrentIndex < 0 ||
            CurrentIndex >= Players.Count)
            return null;

        BalloonPlayer current =
            Players[CurrentIndex];

        if (current.Alive)
            return current;

        // إذا اللاعب الحالي ميت، انتقل للي بعده
        for (int i = 0; i < Players.Count; i++)
        {
            CurrentIndex++;

            if (CurrentIndex >= Players.Count)
                CurrentIndex = 0;

            if (Players[CurrentIndex].Alive)
                return Players[CurrentIndex];
        }

        return null;
    }

    // =========================================================
    // الانتقال للدور التالي
    // =========================================================

    public void NextTurn()
    {
        CancelTimer();

        SelectedOpponent = null;

        WaitingForBalloon = false;

        if (CheckWinner(out _))
            return;

        for (int i = 0; i < Players.Count; i++)
        {
            CurrentIndex++;

            if (CurrentIndex >= Players.Count)
                CurrentIndex = 0;

            if (Players[CurrentIndex].Alive)
            {
                WaitingForOpponent = true;

                StartTimer();

                return;
            }
        }
    }

    // =========================================================
    // فحص الفائز
    // =========================================================

    public bool CheckWinner(
        out BalloonPlayer? winner)
    {
        winner = null;

        List<BalloonPlayer> alive =
            Players
                .Where(p => p.Alive)
                .ToList();

        if (alive.Count == 1)
        {
            winner = alive[0];

            Started = false;
            Finished = true;

            CancelTimer();

            return true;
        }

        return false;
    }

    // =========================================================
    // نص اللاعبين
    // =========================================================

    public string GetPlayersText()
    {
        if (Players.Count == 0)
            return "لا يوجد لاعبين.";

        List<string> lines =
            new List<string>();

        foreach (BalloonPlayer player in Players)
        {
            string balloons =
                string.Concat(
                    Enumerable.Repeat(
                        "🎈",
                        Math.Min(player.Balloons, 7)
                    )
                );

            if (string.IsNullOrEmpty(balloons))
                balloons = "—";

            string status =
                player.Alive
                    ? ""
                    : " ☠️ OUT";

            lines.Add(
                $"{GetNumberEmoji(player.Number)} " +
                $"لاعب {player.Number} — " +
                $"{player.Balloons} 🎈 " +
                $"{balloons}{status}"
            );
        }

        return string.Join(
            "\n",
            lines
        );
    }

    // =========================================================
    // نص الدور
    // =========================================================

    public string GetTurnText()
    {
        BalloonPlayer? current =
            GetCurrentPlayer();

        if (current == null)
            return "❌ لا يوجد لاعب متاح.";

        return
            $"🎯 الدور الآن على لاعب {current.Number}\n\n" +
            $"🎈 عنده: {current.Balloons}\n\n" +
            $"اختار رقم اللاعب الذي تريد تفجير بالوناته.\n" +
            $"⏱️ عندك 15 ثانية.";
    }

    // =========================================================
    // المؤقت
    // =========================================================

    public void StartTimer()
    {
        CancelTimer();

        _timerCancellation =
            new CancellationTokenSource();

        CancellationToken token =
            _timerCancellation.Token;

        _ = RunTimer(token);
    }

    private async Task RunTimer(
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

            if (!Started || Finished)
                return;

            BalloonPlayer? current =
                GetCurrentPlayer();

            if (current == null)
                return;

            WaitingForOpponent = false;
            WaitingForBalloon = false;

            SelectedOpponent = null;

            await SendTimeoutMessage(current);

            NextTurn();

            BalloonPlayer? next =
                GetCurrentPlayer();

            if (next != null &&
                Started &&
                !Finished)
            {
                await SendMessage(
                    GetTurnText()
                );
            }
        }
        catch (TaskCanceledException)
        {
            // طبيعي عند إلغاء المؤقت
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "Timer error: " + ex
            );
        }
    }

    // =========================================================
    // رسالة انتهاء الوقت
    // =========================================================

    private async Task SendTimeoutMessage(
        BalloonPlayer player)
    {
        try
        {
            await SendMessage(
                $"⏰ انتهى الوقت يا لاعب {player.Number}!\n\n" +
                $"تم تخطي دورك ➡️"
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "Timeout message error: " + ex
            );
        }
    }

    // =========================================================
    // إرسال رسالة
    // =========================================================

    private async Task SendMessage(
        string text)
    {
        try
        {
            // نستخدم NextMessge فقط في حال احتجنا الرسائل
            // والإرسال يتم من خلال رسالة موجودة عادةً.
            Console.WriteLine(
                $"[BOT] {text}"
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "Send message error: " + ex
            );
        }

        await Task.CompletedTask;
    }

    // =========================================================
    // إلغاء المؤقت
    // =========================================================

    public void CancelTimer()
    {
        try
        {
            _timerCancellation?.Cancel();
            _timerCancellation?.Dispose();
        }
        catch
        {
        }

        _timerCancellation = null;
    }

    // =========================================================
    // أرقام بشكل إيموجي
    // =========================================================

    private static string GetNumberEmoji(
        int number)
    {
        return number switch
        {
            1 => "1️⃣",
            2 => "2️⃣",
            3 => "3️⃣",
            4 => "4️⃣",
            5 => "5️⃣",
            6 => "6️⃣",
            7 => "7️⃣",
            8 => "8️⃣",
            9 => "9️⃣",
            10 => "🔟",
            _ => number.ToString()
        };
    }
}
