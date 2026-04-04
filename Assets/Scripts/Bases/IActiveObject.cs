using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public interface IActiveObject
{
    bool PendingRemove { get; set; }
    void GameUpdate();
    void GameFixedUpdate();
}


public interface IActiveObject1Sec
{
    void GameUpdate1Sec();
}

public interface IFixedUpdateOnce
{
    int ActiveTag { get; set; }
    void DoFixedUpdate();
}
