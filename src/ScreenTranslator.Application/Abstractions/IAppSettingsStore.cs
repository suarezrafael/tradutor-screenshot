using ScreenTranslator.Domain;

namespace ScreenTranslator.Application.Abstractions;

/// <summary>Loads/persists <see cref="AppSettings"/>. Implemented in Infrastructure as a JSON file under %AppData%.</summary>
public interface IAppSettingsStore
{
    AppSettings Load();

    void Save(AppSettings settings);
}
