using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WolfLive.Api;
using WolfLive.Api.Commands;
using WolfLive.Api.Models;

public class Program
{
    public static IWolfClient? Client;
    public static BalloonGame? Game;

    public static async Task Main(string[] args)
    {
        string email = Environment.GetEnvironmentVariable("WOLF_EMAIL") ?? "";
        string password = Environment.GetEnvironmentVariable("WOLF_PASSWORD") ?? "";

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
        {
            Console.WriteLine("❌ WOLF_EMAIL أو WOLF_PASSWORD غير موجود.");
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
            Console.WriteLine("✅ Connected to Wolf Live!");
        };

        Client.Messaging.OnMessage += async (client, message) =>
        {
            try
            {
                string text = message.Content?.Trim() ?? "";

                Console.WriteLine(
                    $"📩 Message: [{text}] " +
                    $"User={message.UserId} " +
                    $"Group={message.GroupId}");

                // الأرقام تُستخدم أثناء اللعب
                if (int.TryParse(text, out int number))
                {
                    await HandleNumber(client, message, number);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Message error: " + ex.Message);
            }
        };

        Console.WriteLine("🚀 BalloonBot starting...");

        await Client.Login(email, password);

        await Task.Delay(-1);
    }

    public static async Task HandleNumber(
        IWolfClient client,
        Message message,
        int number)
    {
        if (Game == null || !Game.Started)
            return;

        string groupId = message.GroupId ?? "";

        // منع استقبال الأرقام من غرفة ثانية
        if (Game.GroupId != groupId)
            return;

        BalloonPlayer? current = Game.GetCurrentPlayer();

        if (current == null)
            return;

        // فقط صاحب الدور يستطيع اللعب
        if (current.UserId != message.UserId)
            return;

        // اختيار الخصم
        if (Game.WaitingForOpponent)
        {
            BalloonPlayer? opponent = Game.GetPlayerByNumber(number);

            if (opponent == null)
            {
                await message.Reply(
                    client,
                    "❌ رقم اللاعب غير صحيح.\n" +
                    "اختار رقم لاعب موجود.");
                return;
            }

            if (opponent.UserId == current.UserId)
            {
                await message.Reply(
                    client,
                    "❌ ما تگدر تختار نفسك.");
                return;
            }

            if (!opponent.Alive)
            {
                await message.Reply(
                    client,
                    "❌ هذا اللاعب خارج اللعبة.");
                return;
            }

            Game.SelectedOpponentId = opponent.UserId;
            Game.WaitingForOpponent = false;
            Game.WaitingForBalloon = true;

            await message.Reply(
                client,
                $"🎯 تم اختيار: {opponent.Name}\n\n" +
                $"🎈 عنده {opponent.Balloons} بالونات.\n" +
                $"اختار رقم البالون من **1 إلى {opponent.Balloons}**.");

            return;
        }

        // اختيار البالون
        if (Game.WaitingForBalloon)
        {
            if (number < 1)
            {
                await message.Reply(
                    client,
                    "❌ رقم البالون غير صحيح.");
                return;
            }

            BalloonPlayer? opponent =
                Game.GetPlayer(Game.SelectedOpponentId);

            if (opponent == null || !opponent.Alive)
            {
                Game.ResetTurnSelection();

                await message.Reply(
                    client,
                    "❌ الخصم غير متاح.");
                
                await SendTurnMessage(client, message);
                return;
            }

            if (number > opponent.Balloons)
            {
                await message.Reply(
                    client,
                    $"❌ عنده فقط {opponent.Balloons} بالونات.\n" +
                    $"اختار رقم من 1 إلى {opponent.Balloons}.");
                return;
            }

            await ResolveBalloon(client, message, current, opponent, number);
        }
    }

    public static async Task ResolveBalloon(
        IWolfClient client,
        Message message,
        BalloonPlayer current,
        BalloonPlayer opponent,
        int balloonNumber)
    {
        Random random = Random.Shared;
        int chance = random.Next(1, 101);

        // 15% حظ
        if (chance <= 15)
        {
            Game!.ResetTurnSelection();

            await message.Reply(
                client,
                $"🍀 حظ! {current.Name} اختار البالون رقم {balloonNumber}\n\n" +
                $"🎈 البالون ما انفجر!\n" +
                $"🛡️ {opponent.Name} ما خسر أي بالون.\n\n" +
                $"🔄 الدور ينتقل للاعب التالي.");

            Game.NextTurn();

            if (Game.CheckWinner(out BalloonPlayer? winner))
            {
                await FinishGame(client, message, winner!);
                return;
            }

            await SendTurnMessage(client, message);
            return;
        }

        // 15% نجاة
        if (chance <= 30)
        {
            Game!.ResetTurnSelection();

            await message.Reply(
                client,
                $"🛡️ نجاة! {current.Name} اختار البالون رقم {balloonNumber}\n\n" +
                $"🎈 البالون نجا وما انفجر!\n\n" +
                $"🔄 الدور ينتقل للاعب التالي.");

            Game.NextTurn();

            if (Game.CheckWinner(out BalloonPlayer? winner))
            {
                await FinishGame(client, message, winner!);
                return;
            }

            await SendTurnMessage(client, message);
            return;
        }

        // 10% دور إضافي
        if (chance <= 40)
        {
            opponent.Balloons--;

            string result =
                $"🔄 دور إضافي!\n\n" +
                $"👤 {current.Name}\n" +
                $"🎯 اختار {opponent.Name}\n" +
                $"🎈 البالون رقم {balloonNumber} انفجر!\n\n" +
                $"💥 خسر {opponent.Name} بالون واحد.\n" +
                $"🎈 المتبقي عنده: {opponent.Balloons}";

            if (opponent.Balloons <= 0)
            {
                opponent.Balloons = 0;
                opponent.Alive = false;

                result +=
                    $"\n\n💀 {opponent.Name} خرج من اللعبة!";
            }

            await message.Reply(client, result);

            Game!.ResetTurnSelection();

            if (Game.CheckWinner(out BalloonPlayer? winner))
            {
                await FinishGame(client, message, winner!);
                return;
            }

            // لا ننتقل للدور
            await message.Reply(
                client,
                $"🔄 {current.Name} عنده دور إضافي!\n" +
                $"🎯 اختار رقم الخصم.");

            return;
        }

        // 60% انفجار طبيعي
        opponent.Balloons--;

        string normalResult =
            $"💥 انفجار طبيعي!\n\n" +
            $"👤 {current.Name}\n" +
            $"🎯 اختار {opponent.Name}\n" +
            $"🎈 البالون رقم {balloonNumber} انفجر!\n\n" +
            $"📉 خسر {opponent.Name} بالون واحد.\n" +
            $"🎈 المتبقي: {opponent.Balloons}";

        if (opponent.Balloons <= 0)
        {
            opponent.Balloons = 0;
            opponent.Alive = false;

            normalResult +=
                $"\n\n💀 {opponent.Name} خرج من اللعبة!";
        }

        await message.Reply(client, normalResult);

        Game.ResetTurnSelection();

        if (Game.CheckWinner(out BalloonPlayer? normalWinner))
        {
            await FinishGame(client, message, normalWinner!);
            return;
        }

        Game.NextTurn();

        await SendTurnMessage(client, message);
    }

    public static async Task SendTurnMessage(
        IWolfClient client,
        Message message)
    {
        if (Game == null || !Game.Started)
            return;

        BalloonPlayer? current = Game.GetCurrentPlayer();

        if (current == null)
            return;

        await message.Reply(
            client,
            $"🎯 الدور الآن على: {current.Name}\n\n" +
            Game.GetPlayersText() +
            "\n\n" +
            $"👤 {current.Name} اختار رقم الخصم.");
    }

    public static async Task FinishGame(
        IWolfClient client,
        Message message,
        BalloonPlayer winner)
    {
        await message.Reply(
            client,
            $"🏆🎉🎉 انتهت لعبة البالونات! 🎉🎉🏆\n\n" +
            $"🥇 الفائز: {winner.Name}\n" +
            $"🎈 البالونات المتبقية: {winner.Balloons}\n\n" +
            $"🔥 مبروك!");

        Game!.Started = false;
    }
}


// ======================================================
// أوامر لعبة البالونات
// ======================================================

public class BalloonCommands : WolfContext
{
    // أمر واحد فقط لمعالجة كل أوامر البالونات
    [Command("بالونات")]
    public async Task BalloonsCommand(string message)
    {
        string command = message?.Trim() ?? "";

        // !بالونات
        if (string.IsNullOrWhiteSpace(command) ||
            command.Equals("مساعدة", StringComparison.OrdinalIgnoreCase))
        {
            await ShowHelp();
            return;
        }

        // !بالونات جديد
        if (command.Equals("جديد", StringComparison.OrdinalIgnoreCase))
        {
            await NewGame();
            return;
        }

        // !بالونات انضم
        if (command.Equals("انضم", StringComparison.OrdinalIgnoreCase) ||
            command.Equals("انضمام", StringComparison.OrdinalIgnoreCase))
        {
            await JoinGame();
            return;
        }

        // !بالونات لاعبين
        if (command.Equals("لاعبين", StringComparison.OrdinalIgnoreCase))
        {
            await ShowPlayers();
            return;
        }

        // !بالونات بدء
        if (command.Equals("بدء", StringComparison.OrdinalIgnoreCase))
        {
            await StartGame();
            return;
        }

        // !بالونات انهاء
        if (command.Equals("انهاء", StringComparison.OrdinalIgnoreCase) ||
            command.Equals("إنهاء", StringComparison.OrdinalIgnoreCase))
        {
            await EndGame();
            return;
        }

        await ShowHelp();
    }

    private async Task ShowHelp()
    {
        await Reply(
            "🎈🔥 لعبة البالونات 🔥🎈\n\n" +
            "الأوامر:\n\n" +
            "🎮 !بالونات جديد\n" +
            "👤 !بالونات انضم\n" +
            "📋 !بالونات لاعبين\n" +
            "▶️ !بالونات بدء\n" +
            "🛑 !بالونات انهاء\n\n" +
            "🎈 كل لاعب يبدأ بـ 7 بالونات.\n" +
            "🎯 بعد بدء اللعبة تختار رقم الخصم.\n" +
            "🎈 بعدها تختار رقم البالون.\n\n" +
            "🍀 15% حظ\n" +
            "🛡️ 15% نجاة\n" +
            "🔄 10% دور إضافي\n" +
            "💥 60% انفجار طبيعي"
        );
    }

    private async Task NewGame()
    {
        if (Program.Game != null && Program.Game.Started)
        {
            await Reply("❌ توجد لعبة قيد التشغيل بالفعل.");
            return;
        }

        string groupId = Message.GroupId ?? "";

        Program.Game = new BalloonGame(groupId);

        await Reply(
            "🎈🔥 تم إنشاء لعبة البالونات! 🔥🎈\n\n" +
            "👤 كل لاعب يبدأ بـ 7 بالونات.\n\n" +
            "للدخول باللعبة اكتب:\n" +
            "👉 !بالونات انضم"
        );
    }

    private async Task JoinGame()
    {
        if (Program.Game == null)
        {
            await Reply(
                "❌ ماكو لعبة حالياً.\n\n" +
                "🎮 اكتب !بالونات جديد");
            return;
        }

        if (Program.Game.Started)
        {
            await Reply("❌ اللعبة بدأت بالفعل، ما تگدر تنضم الآن.");
            return;
        }

        string userId = Message.UserId;
        string groupId = Message.GroupId ?? "";

        if (Program.Game.GroupId != groupId)
        {
            await Reply("❌ اللعبة منشأة في غرفة ثانية.");
            return;
        }

        string result =
            Program.Game.AddPlayer(userId, groupId);

        await Reply(result);
    }

    private async Task ShowPlayers()
    {
        if (Program.Game == null)
        {
            await Reply(
                "❌ ماكو لعبة حالياً.\n\n" +
                "🎮 اكتب !بالونات جديد");
            return;
        }

        await Reply(Program.Game.GetPlayersText());
    }

    private async Task StartGame()
    {
        if (Program.Game == null)
        {
            await Reply("❌ ماكو لعبة. اكتب !بالونات جديد");
            return;
        }

        if (Program.Game.Started)
        {
            await Reply("❌ اللعبة بدأت بالفعل.");
            return;
        }

        if (Program.Game.Players.Count < 2)
        {
            await Reply(
                "❌ لازم يكون عدد اللاعبين على الأقل 2.\n\n" +
                "👤 خلي اللاعبين يكتبون:\n" +
                "!بالونات انضم");
            return;
        }

        Program.Game.Started = true;
        Program.Game.CurrentPlayerIndex = 0;
        Program.Game.ResetTurnSelection();

        BalloonPlayer? current =
            Program.Game.GetCurrentPlayer();

        await Reply(
            "🚀🔥 بدأت لعبة البالونات! 🔥🚀\n\n" +
            Program.Game.GetPlayersText() +
            "\n\n" +
            $"🎯 أول دور: {current?.Name}\n" +
            $"👤 {current?.Name} اختار رقم الخصم.");
    }

    private async Task EndGame()
    {
        if (Program.Game == null)
        {
            await Reply("❌ ماكو لعبة حالياً.");
            return;
        }

        Program.Game = null;

        await Reply(
            "🛑 تم إنهاء لعبة البالونات.\n\n" +
            "🎈 تگدرون تسوون لعبة جديدة بـ:\n" +
            "!بالونات جديد");
    }
}


// ======================================================
// نظام اللعبة
// ======================================================

public class BalloonGame
{
    public string GroupId { get; }

    public List<BalloonPlayer> Players { get; } =
        new List<BalloonPlayer>();

    public bool Started { get; set; }

    public int CurrentPlayerIndex { get; set; }

    public bool WaitingForOpponent { get; set; }

    public bool WaitingForBalloon { get; set; }

    public string? SelectedOpponentId { get; set; }

    public BalloonGame(string groupId)
    {
        GroupId = groupId;
        Started = false;
        CurrentPlayerIndex = 0;
        ResetTurnSelection();
    }

    public string AddPlayer(
        string userId,
        string groupId)
    {
        if (GroupId != groupId)
            return "❌ ما تگدر تنضم لهذه اللعبة.";

        if (Players.Any(p => p.UserId == userId))
            return "❌ أنت داخل اللعبة بالفعل.";

        if (Players.Count >= 50)
            return "❌ اللعبة وصلت للحد الأقصى 50 لاعب.";

        int number = Players.Count + 1;

        BalloonPlayer player = new BalloonPlayer(
            userId,
            $"لاعب {number}",
            number);

        Players.Add(player);

        return
            $"🎉 تم انضمامك للعبة!\n\n" +
            $"👤 {player.Name}\n" +
            $"🔢 رقمك: {player.Number}\n" +
            $"🎈 بالوناتك: {player.Balloons}\n\n" +
            $"👥 عدد اللاعبين الآن: {Players.Count}";
    }

    public BalloonPlayer? GetPlayer(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return null;

        return Players.FirstOrDefault(
            p => p.UserId == userId);
    }

    public BalloonPlayer? GetPlayerByNumber(int number)
    {
        return Players.FirstOrDefault(
            p => p.Number == number && p.Alive);
    }

    public List<BalloonPlayer> AlivePlayers
    {
        get
        {
            return Players
                .Where(p => p.Alive)
                .ToList();
        }
    }

    public BalloonPlayer? GetCurrentPlayer()
    {
        List<BalloonPlayer> alive = AlivePlayers;

        if (alive.Count == 0)
            return null;

        if (CurrentPlayerIndex < 0 ||
            CurrentPlayerIndex >= alive.Count)
        {
            CurrentPlayerIndex = 0;
        }

        return alive[CurrentPlayerIndex];
    }

    public void NextTurn()
    {
        List<BalloonPlayer> alive = AlivePlayers;

        if (alive.Count == 0)
            return;

        CurrentPlayerIndex++;

        if (CurrentPlayerIndex >= alive.Count)
            CurrentPlayerIndex = 0;

        ResetTurnSelection();
    }

    public void ResetTurnSelection()
    {
        WaitingForOpponent = true;
        WaitingForBalloon = false;
        SelectedOpponentId = null;
    }

    public bool CheckWinner(
        out BalloonPlayer? winner)
    {
        List<BalloonPlayer> alive = AlivePlayers;

        if (alive.Count == 1)
        {
            winner = alive[0];
            return true;
        }

        winner = null;
        return false;
    }

    public string GetPlayersText()
    {
        if (Players.Count == 0)
            return "👥 ماكو لاعبين حالياً.";

        string text = "📋🎈 اللاعبين:\n\n";

        foreach (BalloonPlayer player in Players)
        {
            string balloons = string.Concat(
                Enumerable.Repeat(
                    "🎈",
                    Math.Min(player.Balloons, 7)));

            if (player.Alive)
            {
                text +=
                    $"{NumberEmoji(player.Number)} " +
                    $"{player.Name} — " +
                    $"{player.Balloons} {balloons}\n";
            }
            else
            {
                text +=
                    $"💀 {player.Name} — خارج اللعبة\n";
            }
        }

        return text;
    }

    private static string NumberEmoji(int number)
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
            _ => number + "."
        };
    }
}


// ======================================================
// اللاعب
// ======================================================

public class BalloonPlayer
{
    public string UserId { get; }

    public string Name { get; set; }

    public int Number { get; }

    public int Balloons { get; set; }

    public bool Alive { get; set; }

    public BalloonPlayer(
        string userId,
        string name,
        int number)
    {
        UserId = userId;
        Name = name;
        Number = number;
        Balloons = 7;
        Alive = true;
    }
}
