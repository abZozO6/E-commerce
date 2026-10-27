using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecommerce.Infrastructure_DAL_.Data
{
    public interface IDbConnectionFactory
    {
        SqlConnection CreateConnection();
    }
}
