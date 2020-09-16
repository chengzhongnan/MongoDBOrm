using ProjectCommon.Unit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerCommon.MongoDB
{
    public abstract class DBEntityMgr<TObject> : EntityMgr
        where TObject : MongoObject, new()
    {
        public TObject DBBase { get; set; }

        public DBEntityMgr()
        {
            DBBase = new TObject();
        }

        public override void Init()
        {
            base.Init();           
        }
    }
}
