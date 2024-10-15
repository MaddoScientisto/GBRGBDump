using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GBRGBDump.Web.Shared.Services;

namespace GBRGBDump.Services.Impl;

public class EnvironmentService : IEnvironmentService
{
    public bool IsDesktop => true;
}