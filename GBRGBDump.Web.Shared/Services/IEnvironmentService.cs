﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GBRGBDump.Web.Shared.Services
{
    public interface IEnvironmentService
    {
        bool IsDesktop { get; }
    }
}
