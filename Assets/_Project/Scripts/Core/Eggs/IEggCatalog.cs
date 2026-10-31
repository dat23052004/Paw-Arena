using System.Collections.Generic;

namespace PawArena.Core.Eggs
{
    /// <summary>Provides validated egg definitions to domain and presentation layers.</summary>
    public interface IEggCatalog
    {
        IReadOnlyList<EggDescriptor> Eggs { get; }

        bool TryGet(string eggId, out EggDescriptor egg);
    }
}
