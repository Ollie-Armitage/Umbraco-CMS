﻿using System.Threading.Tasks;

namespace Umbraco.Cms.Core.Persistence.Repositories
{
    public interface IInstallationRepository
    {
        Task SaveInstallLogAsync(InstallLog installLog);
    }
}
