using ProjectCommon.Unit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerCommon.MongoDB
{
    public abstract class DBEntityBase<TObject> : EntityBase
        where TObject : MongoObject, new()
    {
        protected TObject _dbDef { get; set; }

        public DBEntityBase(EntityMgr mgr, short type):base(mgr, type)
        {
            _dbDef = new TObject();
        }

        public override void Init()
        {
            _dbDef.RegisterFields();
        }
    }
}
