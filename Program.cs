using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WolfLive.Api;
using WolfLive.Api.Commands;
using WolfLive.Api.Models.Message;

public class Program
{
    public static WolfClient Client { get; private set; } = null!;

    // الروم الوحيد المسموح للبوت بالعمل داخله
    public const string TargetGroupId = "82041031";

    public static BalloonGame? Game { get; set; }

    public static async Task Main()
    {
        string email = Environment.GetEnvironmentVariable("WOLF_EMAIL") ?? "";
        string password = Environment.GetEnvironmentVariable("WOLF_PASSWORD") ?? "";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            Console.WriteLine("❌ WOLF_EMAIL أو WOLF_PASSWORD غير موجودين.");
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
            Console.WriteLine("✅ Connected to Wolf!");
        };

        Client.Messaging.OnMessage += async message =>
        {
            try
            {
                // تجاهل أي روم غير الروم المحدد
                if (message.GroupId != TargetGroupId)
                    return;

                string text = message.Text?.Trim() ?? "";

                // الأرقام فقط تعتبر اختياراً أثناء اللعبة
                if (int.TryParse(text, out int number))
                {
                    await BalloonGame.HandleNumber(message, number);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Message error: {ex.Message}");
            }
        };

        Console.WriteLine("🔐 تسجيل الدخول...");

        var result = await Client.Login(email, password);

        if (!result)
        {
            Console.WriteLine("❌ فشل تسجيل الدخول.");
            return;
        }

        Console.WriteLine("✅ تم تسجيل الدخول.");

        // الاشتراك بالروم المطلوب فقط
        try
        {
            bool subscribed =
                await Client.Messaging.GroupMessageSubscribe(TargetGroupId);

            Console.WriteLine(
                subscribed
                    ? $"✅ تم الاشتراك بالروم {TargetGroupId}"
                    : "⚠️ لم يتم تأكيد الاشتراك بالروم."
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ خطأ بالاشتراك بالروم: {ex.Message}");
        }

        await Client.Messaging.Initialize();

        Console.WriteLine("🎈 BalloonBot يعمل الآن...");
        Console.WriteLine($"🏠 الروم: {TargetGroupId}");

        await Task.Delay(Timeout.Infinite);
    }
}


// ==========================================================
// أوامر البوت
// ==========================================================

public class BalloonCommands : WolfContext
{
    [Command("بالونات")]
    public async Task BalloonsCommand(string message)
    {
        if (Message.GroupId != Program.TargetGroupId)
            return;

        string[] parts = (message ?? "")
            .Trim()
            .Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries
            );

        if (parts.Length == 0)
        {
            await this.Reply(HelpText());
            return;
        }

        string command = parts[0].ToLower();

        switch (command)
        {
            case "مساعدة":
                await this.Reply(HelpText());
                break;

            case "جديد":
                await NewGame();
                break;

            case "انضم":
            case "انضمام":
                await JoinGame();
                break;

            case "لاعبين":
                await Players();
                break;

            case "بدء":
                await StartGame();
                break;

            case "انهاء":
            case "إنهاء":
                await EndGame();
                break;

            default:
                await this.Reply(HelpText());
                break;
        }
    }


    private string HelpText()
    {
        return
            "🎈 لعبة البالونات 🎈\n\n" +
            "!بالونات جديد — إنشاء لعبة\n" +
            "!بالونات انضم — الانضمام\n" +
            "!بالونات لاعبين — عرض اللاعبين\n" +
            "!بالونات بدء — بدء اللعبة\n" +
            "!بالونات انهاء — إنهاء اللعبة\n\n" +
            "🎈 كل لاعب يبدأ بـ 7 بالونات.\n" +
            "🎯 أثناء الدور أرسل رقم الخصم فقط.\n" +
            "🎈 بعدها أرسل رقم البالونة.";
    }


    private async Task NewGame()
    {
        if (Program.Game != null)
        {
            await this.Reply("⚠️ توجد لعبة قيد الإنشاء أو اللعب.");
            return;
        }

        Program.Game = new BalloonGame(Program.TargetGroupId);

        await this.Reply(
            "🎈 تم إنشاء لعبة البالونات!\n\n" +
            "👥 أرسل:\n" +
            "!بالونات انضم\n\n" +
            "للانضمام إلى اللعبة."
        );
    }


    private async Task JoinGame()
    {
        BalloonGame? game = Program.Game;

        if (game == null)
        {
            await this.Reply(
                "❌ لا توجد لعبة حالياً.\n" +
                "استخدم !بالونات جديد"
            );
            return;
        }

        string userId = Message.UserId;

        string result = game.AddPlayer(
            userId,
            $"لاعب {game.Players.Count + 1}"
        );

        await this.Reply(result);
    }


    private async Task Players()
    {
        BalloonGame? game = Program.Game;

        if (game == null)
        {
            await this.Reply("❌ لا توجد لعبة حالياً.");
            return;
        }

        await this.Reply(game.GetPlayersText());
    }


    private async Task StartGame()
    {
        BalloonGame? game = Program.Game;

        if (game == null)
        {
            await this.Reply("❌ لا توجد لعبة.");
            return;
        }

        string result = game.Start();

        await this.Reply(result);
    }


    private async Task EndGame()
    {
        BalloonGame? game = Program.Game;

        if (game == null)
        {
            await this.Reply("❌ لا توجد لعبة حالياً.");
            return;
        }

        game.Stop();
        Program.Game = null;

        await this.Reply("🛑 تم إنهاء لعبة البالونات.");
    }
}


// ==========================================================
// لعبة البالونات
// ==========================================================

public class BalloonGame
{
    public string GroupId { get; }

    public List<BalloonPlayer> Players { get; } =
        new List<BalloonPlayer>();

    private int CurrentPlayerIndex = 0;

    private bool Running = false;

    private bool WaitingForOpponent = false;

    private bool WaitingForBalloon = false;

    private BalloonPlayer? SelectedOpponent;

    private CancellationTokenSource? TimerCts;


    public BalloonGame(string groupId)
    {
        GroupId = groupId;
    }


    // ------------------------------------------------------
    // إضافة لاعب
    // ------------------------------------------------------

    public string AddPlayer(string userId, string name)
    {
        if (Running)
            return "❌ اللعبة بدأت بالفعل.";

        if (Players.Any(x => x.UserId == userId))
            return "⚠️ أنت موجود بالفعل في اللعبة.";

        if (Players.Count >= 20)
            return "❌ اكتمل عدد اللاعبين.";

        Players.Add(
            new BalloonPlayer
            {
                UserId = userId,
                Name = name,
                Balloons = 7,
                Number = Players.Count + 1
            }
        );

        return
            $"🎈 تم انضمام {name}!\n" +
            $"🎈 عدد بالوناتك: 7\n" +
            $"👥 عدد اللاعبين: {Players.Count}";
    }


    // ------------------------------------------------------
    // عرض اللاعبين
    // ------------------------------------------------------

    public string GetPlayersText()
    {
        if (Players.Count == 0)
            return "❌ لا يوجد لاعبون.";

        string text = "🎈 قائمة اللاعبين 🎈\n\n";

        foreach (var player in Players)
        {
            string balloons = string.Concat(
                Enumerable.Repeat(
                    "🎈",
                    Math.Min(player.Balloons, 7)
                )
            );

            text +=
                $"{player.Number}️⃣ {player.Name} — " +
                $"{player.Balloons} {balloons}\n";
        }

        return text;
    }


    // ------------------------------------------------------
    // بدء اللعبة
    // ------------------------------------------------------

    public string Start()
    {
        if (Running)
            return "⚠️ اللعبة بدأت بالفعل.";

        if (Players.Count < 2)
            return "❌ يجب وجود لاعبين على الأقل.";

        Running = true;

        CurrentPlayerIndex = 0;

        WaitingForOpponent = true;
        WaitingForBalloon = false;

        SelectedOpponent = null;

        StartTimer();

        BalloonPlayer current = Players[CurrentPlayerIndex];

        return
            "🎈🔥 بدأت لعبة البالونات! 🔥🎈\n\n" +
            GetPlayersText() +
            "\n🎯 الدور الآن على:\n" +
            $"👉 {current.Name}\n\n" +
            "أرسل رقم اللاعب الذي تريد ضربه.\n" +
            "⏱️ لديك 15 ثانية.";
    }


    // ------------------------------------------------------
    // استقبال الأرقام
    // ------------------------------------------------------

    public static async Task HandleNumber(Message message, int number)
    {
        BalloonGame? game = Program.Game;

        if (game == null)
            return;

        if (!game.Running)
            return;

        if (message.GroupId != game.GroupId)
            return;

        BalloonPlayer? current = game.GetCurrentPlayer();

        if (current == null)
            return;

        if (message.UserId != current.UserId)
            return;

        if (game.WaitingForOpponent)
        {
            await game.SelectOpponent(message, number);
            return;
        }

        if (game.WaitingForBalloon)
        {
            await game.SelectBalloon(message, number);
            return;
        }
    }


    // ------------------------------------------------------
    // اختيار الخصم
    // ------------------------------------------------------

    private async Task SelectOpponent(Message message, int number)
    {
        BalloonPlayer? current = GetCurrentPlayer();

        if (current == null)
            return;

        if (number < 1 || number > Players.Count)
        {
            await message.Reply(
                Program.Client,
                "❌ رقم اللاعب غير صحيح."
            );

            return;
        }

        BalloonPlayer opponent =
            Players[number - 1];

        if (opponent.UserId == current.UserId)
        {
            await message.Reply(
                Program.Client,
                "❌ لا يمكنك اختيار نفسك."
            );

            return;
        }

        if (opponent.Balloons <= 0)
        {
            await message.Reply(
                Program.Client,
                "❌ هذا اللاعب خرج من اللعبة."
            );

            return;
        }

        SelectedOpponent = opponent;

        WaitingForOpponent = false;
        WaitingForBalloon = true;

        StartTimer();

        await message.Reply(
            Program.Client,
            $"🎯 اخترت {opponent.Name}.\n\n" +
            $"🎈 لديه {opponent.Balloons} بالونات.\n\n" +
            $"أرسل رقم البالونة من 1 إلى {opponent.Balloons}.\n" +
            "⏱️ لديك 15 ثانية."
        );
    }


    // ------------------------------------------------------
    // اختيار البالونة
    // ------------------------------------------------------

    private async Task SelectBalloon(Message message, int balloonNumber)
    {
        BalloonPlayer? current = GetCurrentPlayer();
        BalloonPlayer? opponent = SelectedOpponent;

        if (current == null || opponent == null)
            return;

        if (
            balloonNumber < 1 ||
            balloonNumber > opponent.Balloons
        )
        {
            await message.Reply(
                Program.Client,
                $"❌ اختر رقم من 1 إلى {opponent.Balloons}."
            );

            return;
        }

        CancelTimer();

        WaitingForBalloon = false;

        Random random = new Random();

        int roll = random.Next(1, 101);

        // 15% حظ
        if (roll <= 15)
        {
            WaitingForOpponent = true;

            string text =
                $"🍀 حظ!\n" +
                $"{current.Name} اختار البالونة رقم {balloonNumber} " +
                $"لكنها لم تنفجر!\n\n" +
                "➡️ ينتقل الدور للاعب التالي.";

            MoveToNextPlayer();

            await message.Reply(
                Program.Client,
                text + "\n\n" + CurrentTurnText()
            );

            StartTimer();

            return;
        }


        // 15% نجاة
        if (roll <= 30)
        {
            WaitingForOpponent = true;

            string text =
                $"🛡️ نجاة!\n" +
                $"{opponent.Name} نجا من الانفجار!\n\n" +
                "➡️ ينتقل الدور للاعب التالي.";

            MoveToNextPlayer();

            await message.Reply(
                Program.Client,
                text + "\n\n" + CurrentTurnText()
            );

            StartTimer();

            return;
        }


        // انفجار
        opponent.Balloons--;

        // 10% دور إضافي
        if (roll <= 40)
        {
            if (opponent.Balloons <= 0)
            {
                await message.Reply(
                    Program.Client,
                    $"💥 انفجرت البالونة!\n\n" +
                    $"❌ {opponent.Name} خرج من اللعبة.\n\n" +
                    GetPlayersText()
                );

                if (CheckWinner(out BalloonPlayer? winner))
                {
                    await FinishWinner(message, winner!);
                    return;
                }

                MoveToNextPlayer();
                WaitingForOpponent = true;

                await message.Reply(
                    Program.Client,
                    CurrentTurnText()
                );

                StartTimer();
                return;
            }

            WaitingForOpponent = true;

            await message.Reply(
                Program.Client,
                $"💥 انفجرت البالونة!\n" +
                $"🎈 {opponent.Name} أصبح لديه {opponent.Balloons}.\n\n" +
                $"🔄 دور إضافي لـ {current.Name}!\n\n" +
                CurrentTurnText(current)
            );

            StartTimer();

            return;
        }


        // 60% انفجار طبيعي
        if (opponent.Balloons <= 0)
        {
            await message.Reply(
                Program.Client,
                $"💥💥 انفجرت البالونة!\n\n" +
                $"❌ {opponent.Name} خرج من اللعبة.\n\n" +
                GetPlayersText()
            );

            if (CheckWinner(out BalloonPlayer? winner))
            {
                await FinishWinner(message, winner!);
                return;
            }
        }
        else
        {
            await message.Reply(
                Program.Client,
                $"💥 انفجرت البالونة رقم {balloonNumber}!\n\n" +
                $"🎈 {opponent.Name} أصبح لديه " +
                $"{opponent.Balloons} بالونات.\n\n" +
                GetPlayersText()
            );
        }

        MoveToNextPlayer();

        WaitingForOpponent = true;

        await message.Reply(
            Program.Client,
            CurrentTurnText()
        );

        StartTimer();
    }


    // ------------------------------------------------------
    // الفائز
    // ------------------------------------------------------

    private bool CheckWinner(out BalloonPlayer? winner)
    {
        var alive = Players
            .Where(x => x.Balloons > 0)
            .ToList();

        if (alive.Count == 1)
        {
            winner = alive[0];
            return true;
        }

        winner = null;
        return false;
    }


    private async Task FinishWinner(
        Message message,
        BalloonPlayer winner)
    {
        CancelTimer();

        Running = false;

        WaitingForOpponent = false;
        WaitingForBalloon = false;

        await message.Reply(
            Program.Client,
            "🏆🎉 انتهت اللعبة! 🎉🏆\n\n" +
            $"👑 الفائز هو:\n" +
            $"🔥 {winner.Name} 🔥\n\n" +
            "🎈 بقي معه " +
            $"{winner.Balloons} بالونات.\n\n" +
            "👏 مبروك للفائز!"
        );

        Program.Game = null;
    }


    // ------------------------------------------------------
    // اللاعب التالي
    // ------------------------------------------------------

    private void MoveToNextPlayer()
    {
        if (Players.Count == 0)
            return;

        int attempts = 0;

        do
        {
            CurrentPlayerIndex++;

            if (CurrentPlayerIndex >= Players.Count)
                CurrentPlayerIndex = 0;

            attempts++;

            if (attempts > Players.Count)
                break;

        } while (
            Players[CurrentPlayerIndex].Balloons <= 0
        );

        SelectedOpponent = null;
    }


    // ------------------------------------------------------
    // الدور الحالي
    // ------------------------------------------------------

    private string CurrentTurnText(
        BalloonPlayer? player = null)
    {
        BalloonPlayer? current =
            player ?? GetCurrentPlayer();

        if (current == null)
            return "";

        return
            $"🎯 الدور الآن على {current.Name}\n\n" +
            "أرسل رقم اللاعب الذي تريد ضربه.\n" +
            "⏱️ لديك 15 ثانية.";
    }


    private BalloonPlayer? GetCurrentPlayer()
    {
        if (
            CurrentPlayerIndex < 0 ||
            CurrentPlayerIndex >= Players.Count
        )
            return null;

        return Players[CurrentPlayerIndex];
    }


    // ------------------------------------------------------
    // المؤقت
    // ------------------------------------------------------

    private void StartTimer()
    {
        CancelTimer();

        TimerCts =
            new CancellationTokenSource();

        CancellationToken token =
            TimerCts.Token;

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

                    await TimeoutTurn();
                }
                catch (TaskCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"⚠️ Timer error: {ex.Message}"
                    );
                }
            },
            token
        );
    }


    private void CancelTimer()
    {
        try
        {
            TimerCts?.Cancel();
            TimerCts?.Dispose();
        }
        catch
        {
        }

        TimerCts = null;
    }


    // ------------------------------------------------------
    // انتهاء الوقت
    // ------------------------------------------------------

    private async Task TimeoutTurn()
    {
        if (!Running)
            return;

        BalloonPlayer? current =
            GetCurrentPlayer();

        if (current == null)
            return;

        WaitingForOpponent = false;
        WaitingForBalloon = false;

        SelectedOpponent = null;

        MoveToNextPlayer();

        WaitingForOpponent = true;

        BalloonPlayer? next =
            GetCurrentPlayer();

        if (next == null)
            return;

        try
        {
            await SendToGroup(
                "⏰ انتهى الوقت!\n\n" +
                $"⏭️ تم تجاوز دور {current.Name}.\n\n" +
                CurrentTurnText(next)
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"⚠️ Timeout message error: {ex.Message}"
            );
        }

        StartTimer();
    }


    private async Task SendToGroup(string text)
    {
        // نرسل الرسالة عبر رسالة موجودة فقط عندما تكون متاحة.
        // لا نستخدم SendMessage لأنه غير موجود في IWolfMessaging.
        Console.WriteLine(text);

        await Task.CompletedTask;
    }


    // ------------------------------------------------------
    // إيقاف اللعبة
    // ------------------------------------------------------

    public void Stop()
    {
        Running = false;

        WaitingForOpponent = false;
        WaitingForBalloon = false;

        SelectedOpponent = null;

        CancelTimer();
    }
}


// ==========================================================
// اللاعب
// ==========================================================

public class BalloonPlayer
{
    public string UserId { get; set; } = "";

    public string Name { get; set; } = "";

    public int Number { get; set; }

    public int Balloons { get; set; } = 7;
}
