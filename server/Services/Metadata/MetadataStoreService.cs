using server.Core.IO;
using server.Models.Metadata;

namespace server.Services.Metadata;

public sealed class MetadataStoreService(MetadataDbContext metadataDbContext, TimeProvider timeProvider)
{
    public const string MetaName = "Meta";

    public sealed record Summary(string Locale, string Name, uint Size, string Hash, DateTime UpdatedAt);

    public async ValueTask<MetadataDocument?> FindAsync(string locale, string name, CancellationToken token)
    {
        return await metadataDbContext.MetadataDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(document => document.Locale == locale && document.Name == name, token)
            .ConfigureAwait(false);
    }

    public async ValueTask<List<Summary>> ListAsync(string? locale, CancellationToken token)
    {
        IQueryable<MetadataDocument> query = metadataDbContext.MetadataDocuments.AsNoTracking();
        if (locale is not null)
        {
            query = query.Where(document => document.Locale == locale);
        }

        List<Summary> items = await query
            .OrderBy(document => document.Locale)
            .ThenBy(document => document.Name)
            .Select(document => new Summary(document.Locale, document.Name, document.Size, document.Hash, document.UpdatedAt))
            .ToListAsync(token)
            .ConfigureAwait(false);
        return items;
    }

    public async ValueTask<string> UpsertAsync(string locale, string name, string doc, CancellationToken token)
    {
        string normalized = MetadataHasher.NormalizeLineEndings(doc);
        string hash = MetadataHasher.Hash(normalized);
        uint size = (uint)Encoding.UTF8.GetByteCount(normalized);
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;

        MetadataDocument? existing = await metadataDbContext.MetadataDocuments
            .FirstOrDefaultAsync(document => document.Locale == locale && document.Name == name, token)
            .ConfigureAwait(false);

        if (existing is null)
        {
            await metadataDbContext.MetadataDocuments.AddAsync(
                new MetadataDocument
                {
                    Locale = locale,
                    Name = name,
                    Doc = normalized,
                    Hash = hash,
                    Size = size,
                    UpdatedAt = now,
                },
                token)
                .ConfigureAwait(false);
        }
        else
        {
            existing.Doc = normalized;
            existing.Hash = hash;
            existing.Size = size;
            existing.UpdatedAt = now;
        }

        await metadataDbContext.SaveChangesAsync(token).ConfigureAwait(false);
        await MarkKindStaleAsync(name, now, token).ConfigureAwait(false);
        return hash;
    }

    private async ValueTask MarkKindStaleAsync(string name, DateTime now, CancellationToken token)
    {
        MetadataKindRegistry.Definition? definition = MetadataKindRegistry.FindByName(name);
        if (definition is null)
        {
            return;
        }

        await metadataDbContext.MetadataKinds
            .Where(kind => kind.Kind == definition.Kind && kind.StaleSince == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(kind => kind.StaleSince, now), token)
            .ConfigureAwait(false);
    }

    public async ValueTask<string> RebuildMetaAsync(string locale, CancellationToken token)
    {
        List<Summary> documents = await ListAsync(locale, token).ConfigureAwait(false);
        List<string> flat = documents.Where(document => document.Name != MetaName && !document.Name.Contains('/'))
            .Select(document => document.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
        List<string> scattered = documents.Where(document => document.Name.Contains('/'))
            .Select(document => document.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Dictionary<string, string> hashes = documents
            .Where(document => document.Name != MetaName)
            .ToDictionary(document => document.Name, document => document.Hash, StringComparer.Ordinal);
        Dictionary<string, string> withoutMeta = [];
        foreach (string name in flat.Concat(scattered))
        {
            withoutMeta[name] = hashes[name];
        }

        string self = MetadataHasher.Hash(MetadataHasher.Serialize(withoutMeta));
        List<string> orderedFlat = [.. flat];
        int insertAt = orderedFlat.FindIndex(name => string.CompareOrdinal(name, MetaName) > 0);
        if (insertAt < 0)
        {
            orderedFlat.Add(MetaName);
        }
        else
        {
            orderedFlat.Insert(insertAt, MetaName);
        }

        Dictionary<string, string> manifest = [];
        foreach (string name in orderedFlat.Concat(scattered))
        {
            manifest[name] = name == MetaName ? self : hashes[name];
        }

        return await UpsertAsync(locale, MetaName, MetadataHasher.Serialize(manifest), token).ConfigureAwait(false);
    }
}
