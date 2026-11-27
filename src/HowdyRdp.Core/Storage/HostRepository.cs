using HowdyRdp.Core.Models;

namespace HowdyRdp.Core.Storage;

/// <summary>ホスト一覧 (hosts.json)。パスワードは含まない。</summary>
public sealed class HostRepository
{
    private readonly string _path;
    private List<HostEntry> _hosts;

    public HostRepository(AppPaths paths)
    {
        _path = paths.HostsFile;
        _hosts = JsonFile.Read<HostFileDocument>(_path)?.Hosts ?? [];
    }

    public IReadOnlyList<HostEntry> All => _hosts;

    public HostEntry? Find(Guid id) => _hosts.FirstOrDefault(h => h.Id == id);

    public void Upsert(HostEntry host)
    {
        var index = _hosts.FindIndex(h => h.Id == host.Id);
        if (index >= 0)
        {
            _hosts[index] = host;
        }
        else
        {
            _hosts.Add(host);
        }

        Save();
    }

    public bool Remove(Guid id)
    {
        var removed = _hosts.RemoveAll(h => h.Id == id) > 0;
        if (removed)
        {
            Save();
        }

        return removed;
    }

    public void Save() => JsonFile.Write(_path, new HostFileDocument { Hosts = _hosts });

    private sealed class HostFileDocument
    {
        public int Version { get; set; } = 1;

        public List<HostEntry> Hosts { get; set; } = [];
    }
}
