using NotesApp.Models;
using NotesApp.Services;
using StackExchange.Redis;

var redisConnectionString = Environment.GetEnvironmentVariable("REDIS_CONNECTION")
                            ?? "localhost:6379";

Console.WriteLine($"Redis'e baglaniliyor: {redisConnectionString} ...");

ConnectionMultiplexer redis;

try
{
    redis = await ConnectionMultiplexer.ConnectAsync(redisConnectionString);
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("Redis'e baglanilamadi!");
    Console.WriteLine($"Hata: {ex.Message}");
    Console.ResetColor();
    Console.WriteLine();
    Console.WriteLine("Redis'in calistigindan emin olun:");
    Console.WriteLine("  docker compose up -d");
    return 1;
}

Console.OutputEncoding = System.Text.Encoding.UTF8;
var noteService = new NoteService(redis);

Console.Clear();
PrintBanner();

var running = true;
while (running)
{
    PrintMenu();
    Console.Write("Seçiminiz: ");
    var choice = Console.ReadLine()?.Trim();

    Console.WriteLine();

    switch (choice)
    {
        case "1":
            await AddNoteAsync(noteService);
            break;
        case "2":
            await ListNotesAsync(noteService);
            break;
        case "3":
            await ViewNoteAsync(noteService);
            break;
        case "4":
            await UpdateNoteAsync(noteService);
            break;
        case "5":
            await DeleteNoteAsync(noteService);
            break;
        case "0":
        case "q":
        case "Q":
            running = false;
            break;

        default:
            PrintWarning("Geçersiz seçim. Lütfen tekrar deneyin.");
            break;
    }

    if (running)
    {
        Console.WriteLine();
        Console.WriteLine("Devam etmek için bir tuşa basın...");
        Console.ReadKey(true);
        Console.Clear();
        PrintBanner();
    }
}

Console.WriteLine("Görüsmek üzere!");
redis.Dispose();
return 0;

static void PrintBanner()
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("=========================");
    Console.WriteLine(" NotesAPP - Redis Not Defteri");
    Console.WriteLine("=============================");
    Console.ResetColor();
    Console.WriteLine();

}

static void PrintMenu()
{
    Console.WriteLine("1) Yeni not ekle");
    Console.WriteLine("2) Tüm notları listele");
    Console.WriteLine("3) Not detayını gör");
    Console.WriteLine("4) Not güncelle");
    Console.WriteLine("5) Not Sil");
    Console.WriteLine("0) Çıkış");
    Console.WriteLine();


}

static void PrintSuccess(string message)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"[OK] {message}");
    Console.ResetColor();

}

static void PrintWarning(string message)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"[OK] {message}");
    Console.ResetColor();

}
static void PrintError(string message)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"[OK] {message}");
    Console.ResetColor();

}

static void PrintNoteSummary(Note note)
{
    Console.ForegroundColor = ConsoleColor.Gray;
    Console.Write($"[{note.Id}] ");
    Console.ResetColor();
    Console.ForegroundColor = ConsoleColor.White;
    Console.Write(note.Title);
    Console.ResetColor();
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine($"  ({note.CreatedAt.ToLocalTime():dd.MM.yyyy HH:mm})");
    Console.ResetColor();
}


//komut handler'ları

static async Task AddNoteAsync(NoteService service)
{
    Console.WriteLine("---- YENİ NOT ----");

    Console.WriteLine("Başlık: ");
    var title = Console.ReadLine()?.Trim() ?? string.Empty;
    if (string.IsNullOrWhiteSpace(title))
    {
        PrintWarning("Başlık boş olamaz, vazgeçildi.");
        return;

    }

    Console.WriteLine("İçerik (boş satır bırakarak bitirin): ");
    var contentLines = new List<string>();

    while (true)
    {
        var line = Console.ReadLine();
        if (string.IsNullOrEmpty(line)) break;
        contentLines.Add(line);
    }
    var content = string.Join("\n", contentLines);
    var note = await service.CreateAsync(title, content);
    PrintSuccess($"Not eklendi. Id: {note.Id}");

}

static async Task ListNotesAsync(NoteService service)
{
    Console.WriteLine("---Notlar---");

    var notes = await service.GetAllAsync();
    if (notes.Count == 0)
    {
        PrintWarning("Henüz hiç not yok.");
        return;
    }

    foreach (var note in notes)

        PrintNoteSummary(note);
    Console.WriteLine();
    Console.WriteLine($"Toplam: {notes.Count} not");

}

static async Task ViewNoteAsync(NoteService service)
{
    Console.Write("Görüntülenecek not Id: ");
    var id = Console.ReadLine()?.Trim();
    if (string.IsNullOrWhiteSpace(id))
    {
        PrintWarning("Id boş olamaz.");
        return;
    }

    var note = await service.GetAsync(id);
    if (note is null)
    {
        PrintError($"'{id} id'li not bulunamadı");
        return;
    }

    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($">> {note.Title}");
    Console.ResetColor();
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine($"Id: {note.Id}");
    Console.WriteLine($"Oluşturuldu: {note.CreatedAt.ToLocalTime():dd.MM.yyyy HH:mm}");
    Console.WriteLine($"Güncellendi: {note.UpdatedAt.ToLocalTime():dd.MM.yyyy HH:mm}");
    Console.ResetColor();
    Console.WriteLine(new string('-', 40));
    Console.WriteLine(string.IsNullOrEmpty(note.Content) ? "(içerik boş)" : note.Content);
}

static async Task UpdateNoteAsync(NoteService service)
{
    Console.Write("Güncellenecek not Id: ");
    var id = Console.ReadLine()?.Trim();
    if (string.IsNullOrWhiteSpace(id))
    {
        PrintWarning("Id boş olamaz.");
        return;
    }

    var existing = await service.GetAsync(id);
    if (existing is null)
    {
        PrintError($"'{id} id'li not obulunamadı.");
        return;

    }

    Console.WriteLine($"Mevcut başlık: {existing.Title}");
    Console.Write("Yeni başlık (boş bırakırsanız değişmez): ");

    var newTitle = Console.ReadLine();

    Console.WriteLine("Mevcut içerik: ");
    Console.WriteLine(string.IsNullOrEmpty(existing.Content) ? "(boş)" : existing.Content);

    Console.WriteLine();

    Console.Write("İçeriği değiştirmek istiyor musunuz ? (e/h)");

    var changeContent = Console.ReadLine()?.Trim().ToLowerInvariant();

    string? newContent = null;
    if (changeContent == "e" || changeContent == "evet" || changeContent == "y" || changeContent == "yes")
    {
        Console.WriteLine("Yeni içerik boş satır bırakarak bitirin: ");
        var lines = new List<string>();
        while (true)
        {
            var line = Console.ReadLine();
            if (string.IsNullOrEmpty(line)) break;
            lines.Add(line);
        }
        newContent = string.Join("\n, lines");
    }

    var ok = await service.UpdateAsync(id, newTitle, newContent);
    if (ok) PrintSuccess("Not Güncellendi");
    else PrintError("Güncelleme BAŞARISIZ");

}

static async Task DeleteNoteAsync(NoteService service)
{
    Console.Write("Silinecek not Id: ");
    var id = Console.ReadLine()?.Trim();
    if (string.IsNullOrWhiteSpace(id))
    {
        PrintWarning("Id boş olamaz.ç");
        return;

    }

    var existing = await service.GetAsync(id);
    if (existing is null)
    {
        PrintError($"'{id}' id'li not bulunamadı.");
        return;
    }

    Console.Write($"'{existing.Title}' silinsin mi? (e/h): ");
    var confirm = Console.ReadLine()?.Trim().ToLowerInvariant();
    if (confirm != "e" && confirm != "evet" && confirm != "y" && confirm != "tes")
    {
        PrintWarning("vazgecildi. ");
        return;
    }
    var ok = await service.DeleteAsync(id);
    if (ok) PrintSuccess("not silindi.");
    else PrintError("silme başarısız.");
}