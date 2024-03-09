﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Core.Inventory
{
    public interface IInventoryAccessor
    {
        Label InventoryGet();
        void InventoryReturn(Label label);
        void InventoryDrop(Label label);
    }
}
