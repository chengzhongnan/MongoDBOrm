using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerCommon.MongoDB
{

    /// <summary>
    /// 字段类型
    /// </summary>
    public enum eFieldType
    {
        /// <summary>
        /// double
        /// </summary>
        Double,
        /// <summary>
        /// string
        /// </summary>
        String,
        /// <summary>
        /// Bson.ObjectId
        /// </summary>
        ObjectId,
        /// <summary>
        /// bool
        /// </summary>
        Bool,
        /// <summary>
        /// DateTime (local)
        /// </summary>
        DateTime,
        /// <summary>
        /// 32-bit integer
        /// </summary>
        Int32,
        /// <summary>
        /// 64-bit integer
        /// </summary>
        Int64,
        /// <summary>
        /// byte[]
        /// </summary>
        Binary,

        /// <summary>
        /// document
        /// </summary>
        Object,
        /// <summary>
        /// array
        /// </summary>
        Array,
        /// <summary>
        /// Dictionary
        /// </summary>
        Map,
    }
}
