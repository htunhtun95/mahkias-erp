using System;
using System.Collections.Generic;
using System.Text;

namespace Mahkias.Core.Data
{

    public interface IRepositoryProvider
    {
         

        string CoreConnectionString { get; set; }
         

        
        T GetAdoRepository<T>() where T : class;


        /// <summary>
        /// Set the repository to return from this provider.
        /// </summary>
        /// <remarks>
        /// Set a repository if you don't want this provider to create one.
        /// Useful in testing and when developing without a backend
        /// implementation of the object returned by a repository of type T.
        /// </remarks>
        void SetRepository<T>(T repository);
    }
}
