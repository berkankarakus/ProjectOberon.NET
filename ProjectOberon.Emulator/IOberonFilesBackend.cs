namespace ProjectOberon.Emulator;

internal interface IOberonFilesBackend
{
    OberonFile? Old(string name);
    OberonFile New(string name);
    void Register(OberonFile file);
    bool Delete(string name);
    bool Rename(string oldName, string newName);
    void Purge(OberonFile file);
    void Close(OberonFile file);
}
