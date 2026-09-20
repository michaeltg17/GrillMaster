using System;
using System.Collections.Generic;
using System.Text;

namespace GrillMaster.Application
{
    public class GrillMasterException(string message) : Exception(message)
    {
    }
}
