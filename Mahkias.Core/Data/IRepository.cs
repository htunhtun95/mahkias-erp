using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Mahkias.Core.Data
{

    public interface IRepository<T> where T : class
    { 
        Task<T> GetByIdAsync(string id);
        Task AddAsync(T entity); 
        Task Update(T entity); 
        Task DeleteAsync(string id);
    }
}
