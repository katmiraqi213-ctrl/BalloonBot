using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WolfLive.Api;
using WolfLive.Api.Commands;
using WolfLive.Api.Models;

public class Program
{
private static IWolfClient _client = null!;

// اللعبة الحالية
internal static BalloonGame? Game;

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

    _client = new WolfClient()
        .SetupCommands()
        .WithCommandSet(c =>
        {
            c.AddCommands<BalloonCommands>()
             .WithPrefix("!");
        })
        .WithSerilog()
        .Done();

    _client.OnConnected += (_) =>
    {
        Console.WriteLine("✅ Connected to wolf.live!");
    };

    // استقبال الرسائل العادية، ومنها أرقام اختيار اللاعب والبالون
    _client.Messaging.OnMessage += async (client, message) =>
    {
        try
        {
            string text = message.Content?.Trim() ?? "";

            Console.WriteLine(
                $"📩 Message: [{text}] User={message.UserId} Group={message.GroupId}");

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

    bool result = await _client.Login(email, password);

    Console.WriteLine(
        $"Login {(result ? "success!" : "failed!")}");

    if (!result)
    {
        return;
    }

    await Task.Delay(-1);
}

private static async Task HandleNumber(
    IWolfClient client,
    Message message,
    int number)
{
    if (Game == null)
        return;

    // الأرقام تعمل فقط داخل غرفة اللعبة
    if (!string.IsNullOrEmpty(Game.GroupId) &&
        message.GroupId != Game.GroupId)
    {
        return;
    }

    if (!Game.Started)
    {
        return;
    }

    if (number < 1)
    {
        return;
    }

    var player = Game.GetPlayer(message.UserId);

    if (player == null)
    {
        await message.Reply(
            client,
            "❌ أنت مو مشترك باللعبة.\nاكتب: !بالونات انضم");
        return;
    }

    if (player.Eliminated)
    {
        await message.Reply(
            client,
            "❌ أنت خارج اللعبة.");
        return;
    }

    if (Game.CurrentPlayerId != player.UserId)
    {
        return;
    }

    // المرحلة الأولى: اختيار الخصم
    if (Game.WaitingForOpponent)
    {
        if (number < 1 || number > Game.AlivePlayers.Count)
        {
            await message.Reply(
                client,
                $"❌ اختار رقم لاعب من 1 إلى {Game.AlivePlayers.Count}.");
            return;
        }

        var opponent = Game.GetPlayerByNumber(number);

        if (opponent == null || opponent.Eliminated)
        {
            await message.Reply(
                client,
                "❌ هذا اللاعب غير موجود أو خارج اللعبة.");
            return;
        }

        if (opponent.UserId == player.UserId)
        {
            await message.Reply(
                client,
                "❌ ما تگدر تختار نفسك.");
            return;
        }

        Game.SelectedOpponentId = opponent.UserId;
        Game.WaitingForOpponent = false;
        Game.WaitingForBalloon = true;

        await message.Reply(
            client,
            $"🎯 اخترت: {opponent.Name}\n\n" +
            $"🎈 عنده {opponent.Balloons} بالونات.\n" +
            $"اختار رقم البالون من 1 إلى {opponent.Balloons}.");
        return;
    }

    // المرحلة الثانية: اختيار البالون
    if (Game.WaitingForBalloon)
    {
        if (Game.SelectedOpponentId == null)
            return;

        var opponent = Game.GetPlayer(Game.SelectedOpponentId);

        if (opponent == null || opponent.Eliminated)
        {
            Game.ResetTurnSelection();
            await message.Reply(
                client,
                "❌ الخصم لم يعد متاحاً.");
            return;
        }

        if (number < 1 || number > opponent.Balloons)
        {
            await message.Reply(
                client,
                $"❌ اختار رقم بالون من 1 إلى {opponent.Balloons}.");
            return;
        }

        await ResolveBalloon(
            client,
            message,
            player,
            opponent,
            number);
    }
}

private static async Task ResolveBalloon(
    IWolfClient client,
    Message message,
    BalloonPlayer attacker,
    BalloonPlayer opponent,
    int balloonNumber)
{
    Game!.ResetTurnSelection();

    int roll = Random.Shared.Next(1, 101);

    // 15% حظ
    if (roll <= 15)
    {
        await message.Reply(
            client,
            $"🍀 حظ!\n\n" +
            $"🎈 البالون رقم {balloonNumber} ما انفجر!\n" +
            $"😎 {opponent.Name} نجا.\n\n" +
            $"➡️ الدور ينتقل للاعب التالي.");

        Game.NextTurn();
        await SendTurnMessage(client, opponent.GroupId);
        return;
    }

    // 15% نجاة
    if (roll <= 30)
    {
        await message.Reply(
            client,
            $"🛡️ نجاة!\n\n" +
            $"🎈 البالون رقم {balloonNumber} بقي مكانه.\n" +
            $"👏 {opponent.Name} نجا من الانفجار.\n\n" +
            $"➡️ الدور ينتقل للاعب التالي.");

        Game.NextTurn();
        await SendTurnMessage(client, opponent.GroupId);
        return;
    }

    // 10% دور إضافي
    if (roll <= 40)
    {
        opponent.Balloons--;

        if (opponent.Balloons <= 0)
        {
            opponent.Balloons = 0;
            opponent.Eliminated = true;

            await message.Reply(
                client,
                $"💥💥 انفجار قوي!\n\n" +
                $"🎈 البالون رقم {balloonNumber} انفجر!\n" +
                $"❌ {opponent.Name} خسر آخر بالون.\n\n" +
                $"🔥 لكنه كان دوراً إضافياً!");

            if (Game.CheckWinner(out BalloonPlayer? winner))
            {
                await message.Reply(
                    client,
                    $"🏆🎉 انتهت اللعبة!\n\n" +
                    $"🥇 الفائز: {winner!.Name}\n" +
                    $"🎈 بقي معه: {winner.Balloons} بالونات.");

                Game.Started = false;
                return;
            }

            await message.Reply(
                client,
                $"🔥 {attacker.Name} يحصل على دور إضافي!");

            await SendTurnMessage(client, opponent.GroupId);
            return;
        }

        await message.Reply(
            client,
            $"🔄 دور إضافي!\n\n" +
            $"💥 البالون رقم {balloonNumber} انفجر!\n" +
            $"❌ {opponent.Name} خسر بالوناً.\n\n" +
            $"🎈 المتبقي عنده: {opponent.Balloons}\n" +
            $"🔥 {attacker.Name} عنده دور إضافي!");

        await SendTurnMessage(client, opponent.GroupId);
        return;
    }

    // 60% انفجار طبيعي
    opponent.Balloons--;

    if (opponent.Balloons < 0)
        opponent.Balloons = 0;

    if (opponent.Balloons == 0)
    {
        opponent.Eliminated = true;

        await message.Reply(
            client,
            $"💥🎈 انفجر البالون رقم {balloonNumber}!\n\n" +
            $"❌ {opponent.Name} خسر آخر بالون.\n" +
            $"☠️ تم إقصاؤه من اللعبة.");

        if (Game.CheckWinner(out BalloonPlayer? winner))
        {
            await message.Reply(
                client,
                $"🏆🎉🎉 انتهت اللعبة!\n\n" +
                $"🥇 الفائز هو: {winner!.Name}\n" +
                $"🎈 بالونات الفائز: {winner.Balloons}");

            Game.Started = false;
            return;
        }

        Game.NextTurn();

        await SendTurnMessage(
            client,
            opponent.GroupId);

        return;
    }

    await message.Reply(
        client,
        $"💥🎈 انفجر البالون رقم {balloonNumber}!\n\n" +
        $"❌ {opponent.Name} خسر بالوناً.\n" +
        $"🎈 المتبقي: {opponent.Balloons}\n\n" +
        $"➡️ الدور ينتقل للاعب التالي.");

    Game.NextTurn();

    await SendTurnMessage(
        client,
        opponent.GroupId);
}

internal static async Task SendTurnMessage(
    IWolfClient client,
    string? groupId)
{
    if (Game == null || !Game.Started)
        return;

    var current = Game.GetCurrentPlayer();

    if (current == null)
        return;

    string players = Game.GetPlayersText();

    string text =
        $"🎈🔥 لعبة البالونات 🔥🎈\n\n" +
        $"{players}\n\n" +
        $"🎯 الدور الآن على: {current.Name}\n\n" +
        $"👤 اختار رقم الخصم من القائمة.\n" +
        $"مثال: 2";

    await Game.SendToGroup(client, groupId, text);
}

}

public class BalloonCommands : WolfContext
{
[Command("بالونات")]
public async Task Help(string message)
{
await this.Reply(
"🎈🔥 لعبة البالونات 🔥🎈\n\n" +
"الأوامر:\n" +
"🎮 !بالونات جديد — إنشاء لعبة\n" +
"👤 !بالونات انضم — الانضمام\n" +
"📋 !بالونات لاعبين — عرض اللاعبين\n" +
"▶️ !بالونات بدء — بدء اللعبة\n" +
"🛑 !بالونات انهاء — إنهاء اللعبة\n\n" +
"كل لاعب يبدأ بـ 7 🎈\n" +
"بعد بدء اللعبة تختار رقم الخصم، ثم رقم البالون.");
}

[Command("بالونات مساعدة")]
public async Task Help2(string message)
{
    await Help(message);
}

[Command("بالونات جديد")]
public async Task NewGame(string message)
{
    if (Program.Game != null &&
        Program.Game.Started)
    {
        await this.Reply(
            "❌ توجد لعبة بالونات قيد التشغيل حالياً.");
        return;
    }

    Program.Game = new BalloonGame();

    await this.Reply(
        "🎈🔥 تم إنشاء لعبة البالونات!\n\n" +
        "👤 كل لاعب يبدأ بـ 7 بالونات.\n\n" +
        "للانضمام اكتب:\n" +
        "👉 !بالونات انضم\n\n" +
        "بعد اكتمال اللاعبين اكتب:\n" +
        "👉 !بالونات بدء");
}

[Command("بالونات انضم")]
public async Task Join(string message)
{
    if (Program.Game == null)
    {
        await this.Reply(
            "❌ ماكو لعبة حالياً.\nاكتب !بالونات جديد");
        return;
    }

    if (Program.Game.Started)
    {
        await this.Reply(
            "❌ اللعبة بدأت بالفعل.");
        return;
    }

    // ملاحظة:
    // Commands framework يمرر النص للـ command.
    // لذلك نستخدم معرف السياق من WolfContext إذا كان متاحاً.
    string userId = GetContextUserId();
    string groupId = GetContextGroupId();

    if (string.IsNullOrWhiteSpace(userId))
    {
        await this.Reply(
            "❌ تعذر الحصول على معرف اللاعب.");
        return;
    }

    if (string.IsNullOrWhiteSpace(groupId))
    {
        await this.Reply(
            "❌ يجب تشغيل اللعبة داخل روم جماعي.");
        return;
    }

    var result = Program.Game.AddPlayer(
        userId,
        groupId);

    await this.Reply(result);
}

[Command("بالونات انضمام")]
public async Task Join2(string message)
{
    await Join(message);
}

[Command("بالونات لاعبين")]
public async Task Players(string message)
{
    if (Program.Game == null)
    {
        await this.Reply(
            "❌ ماكو لعبة حالياً.");
        return;
    }

    await this.Reply(
        Program.Game.GetPlayersText());
}

[Command("بالونات بدء")]
public async Task Start(string message)
{
    if (Program.Game == null)
    {
        await this.Reply(
            "❌ ماكو لعبة.\nاكتب !بالونات جديد");
        return;
    }

    if (Program.Game.Started)
    {
        await this.Reply(
            "❌ اللعبة بدأت بالفعل.");
        return;
    }

    if (Program.Game.Players.Count < 2)
    {
        await this.Reply(
            "❌ لازم يكون هناك لاعبين اثنين على الأقل.");
        return;
    }

    Program.Game.Started = true;
    Program.Game.CurrentPlayerIndex = 0;

    await this.Reply(
        "🎈🔥🔥 بدأت لعبة البالونات! 🔥🔥🎈\n\n" +
        Program.Game.GetPlayersText());

    await Program.SendTurnMessage(
        GetContextClient(),
        Program.Game.GroupId);
}

[Command("بالونات انهاء")]
public async Task Stop(string message)
{
    if (Program.Game == null)
    {
        await this.Reply(
            "❌ ماكو لعبة حالياً.");
        return;
    }

    Program.Game = null;

    await this.Reply(
        "🛑 تم إنهاء لعبة البالونات.");
}

[Command("بالونات إنهاء")]
public async Task Stop2(string message)
{
    await Stop(message);
}

// هذه الدوال تعتمد على أعضاء WolfContext الموجودة بالمكتبة.
// إذا كان إصدار المكتبة يعطي السياق بهذه الأسماء، يتم استخدامها مباشرة.
private string GetContextUserId()
{
    return context.Message.UserId;
}

private string GetContextGroupId()
{
    return context.Message.GroupId ?? "";
}

private IWolfClient GetContextClient()
{
    return context.Client;
}

}

public class BalloonGame
{
public string GroupId { get; private set; } = "";

public bool Started { get; set; }

public int CurrentPlayerIndex { get; set; }

public bool WaitingForOpponent { get; set; } = true;

public bool WaitingForBalloon { get; set; }

public string? SelectedOpponentId { get; set; }

public List<BalloonPlayer> Players { get; } =
    new List<BalloonPlayer>();

public List<BalloonPlayer> AlivePlayers =>
    Players
        .Where(p => !p.Eliminated)
        .ToList();

public string? CurrentPlayerId
{
    get
    {
        var player = GetCurrentPlayer();
        return player?.UserId;
    }
}

public string AddPlayer(
    string userId,
    string groupId)
{
    if (Players.Any(p => p.UserId == userId))
    {
        return "❌ أنت مشترك بالفعل باللعبة.";
    }

    if (Players.Count >= 50)
    {
        return "❌ وصلت اللعبة للحد الأقصى من اللاعبين.";
    }

    if (Players.Count == 0)
    {
        GroupId = groupId;
    }

    if (GroupId != groupId)
    {
        return "❌ هذه اللعبة مرتبطة بروم آخر.";
    }

    string name = $"لاعب {Players.Count + 1}";

    Players.Add(
        new BalloonPlayer(
            userId,
            name));

    return
        $"✅ تم انضمامك للعبة!\n\n" +
        $"👤 اسمك: {name}\n" +
        $"🎈 بالوناتك: 7\n\n" +
        GetPlayersText();
}

public BalloonPlayer? GetPlayer(string userId)
{
    return Players.FirstOrDefault(
        p => p.UserId == userId);
}

public BalloonPlayer? GetPlayerByNumber(int number)
{
    if (number < 1 || number > AlivePlayers.Count)
        return null;

    return AlivePlayers[number - 1];
}

public BalloonPlayer? GetCurrentPlayer()
{
    var alive = AlivePlayers;

    if (alive.Count == 0)
        return null;

    if (CurrentPlayerIndex >= alive.Count)
        CurrentPlayerIndex = 0;

    return alive[CurrentPlayerIndex];
}

public void NextTurn()
{
    var alive = AlivePlayers;

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
    var alive = AlivePlayers;

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
        return "👥 لا يوجد لاعبين.";

    var alive = AlivePlayers;

    var lines = new List<string>();

    for (int i = 0; i < alive.Count; i++)
    {
        var player = alive[i];

        string balloons =
            new string('🎈', Math.Min(player.Balloons, 7));

        lines.Add(
            $"{NumberEmoji(i + 1)} {player.Name} — " +
            $"{player.Balloons} {balloons}");
    }

    var eliminated =
        Players
            .Where(p => p.Eliminated)
            .ToList();

    if (eliminated.Count > 0)
    {
        lines.Add("");
        lines.Add("☠️ المقصيون:");

        foreach (var player in eliminated)
        {
            lines.Add(
                $"❌ {player.Name} — 0 🎈");
        }
    }

    return string.Join("\n", lines);
}

public async Task SendToGroup(
    IWolfClient client,
    string? groupId,
    string text)
{
    if (string.IsNullOrWhiteSpace(groupId))
        return;

    // نستخدم آخر رسالة متاحة لإرسال الرد.
    // هذه الدالة لا ترسل رسالة جديدة من تلقاء نفسها.
    await Task.CompletedTask;
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
        _ => $"{number}."
    };
}

}

public class BalloonPlayer
{
public string UserId { get; }

public string Name { get; }

public int Balloons { get; set; } = 7;

public bool Eliminated { get; set; }

public BalloonPlayer(
    string userId,
    string name)
{
    UserId = userId;
    Name = name;
}

}
