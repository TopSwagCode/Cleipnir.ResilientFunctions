﻿using System.Threading.Tasks;
using Cleipnir.ResilientFunctions.Domain;

namespace Cleipnir.ResilientFunctions.Watchdogs.Invocation;

internal delegate Task<RResult> RAction(object param, RScrapbook? scrapbook);