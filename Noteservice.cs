using System.Net.Mime;
using System.Text.Json;
using NotesApp.Models;
using StackExchange.Redis;

namespace NotesApp.Services;

public class NoteService
{
    private const string NoteKeyPrefix = "note:";
    private const string NotesIdsKey = "notes:ids";

    private readonly IDatabase _db;

    public NoteService(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    private static string BuildKey(string id) => $"{NoteKeyPrefix}{id}";
    public async Task<Note> CreateAsync(string title, string content)
    {
        var note = new Note
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Title = title,
            Content = content,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(note);

        var tran = _db.CreateTransaction();
        _ = tran.StringSetAsync(BuildKey(note.Id), json);
        _ = tran.SetAddAsync(NotesIdsKey, note.Id);
        var committed = await tran.ExecuteAsync();

        if (!committed)
            throw new InvalidOperationException("Not Redis'e kayır edilmedi.");
        return note;
    }

    public async Task<Note?> GetAsync(string id)
    {
        var value = await _db.StringGetAsync(BuildKey(id));
        if (value.IsNullOrEmpty)
            return null;

        return JsonSerializer.Deserialize<Note>(value!);
        // tümmm notları getirir.
    }

    public async Task<List<Note>> GetAllAsync()
    {
        var idValues = await _db.SetMembersAsync(NotesIdsKey);
        if (idValues.Length == 0)
            return new List<Note>();

        var keys = idValues.Select(v => (RedisKey)BuildKey(v.ToString())).ToArray();

        var values = await _db.StringGetAsync(keys);

        var notes = new List<Note>(values.Length);
        foreach (var value in values)
        {
            if (value.IsNullOrEmpty) continue;
            var note = JsonSerializer.Deserialize<Note>(value!);
            if (note != null) notes.Add(note);
        }

        return notes.OrderByDescending(n => n.CreatedAt).ToList();
    }

    public async Task<bool> UpdateAsync(string id, string? newTitle, string? newContent)
    {
        var existing = await GetAsync(id);
        if (existing is null) return false;

        if (!string.IsNullOrWhiteSpace(newTitle))
            existing.Title = newTitle;

        if (newContent is not null) // icerigi bos stringe set etmeye izin verir

            existing.Content = newContent;

        existing.UpdatedAt = DateTime.UtcNow;

        var json = JsonSerializer.Serialize(existing);
        return await _db.StringSetAsync(BuildKey(id), json);


    }

    public async Task<bool> DeleteAsync(string id)
    {
        var tran = _db.CreateTransaction();
        var deleteTask = tran.KeyDeleteAsync(BuildKey(id));
        _ = tran.SetRemoveAsync(NotesIdsKey, id);

        var committed = await tran.ExecuteAsync();
        if (!committed) return false;

        return await deleteTask;

    }

    public async Task ClearAllAsync()
    {
        var ids = await _db.SetMembersAsync(NotesIdsKey);
        if (ids.Length == 0) return;

        var keys = ids.Select(v => (RedisKey)BuildKey(v.ToString())).ToArray();

        await _db.KeyDeleteAsync(keys);
        await _db.KeyDeleteAsync(NotesIdsKey);
    }
}