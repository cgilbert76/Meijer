﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Meijer.Interface
{
    public interface IChaosClient
    {
        Task<HttpResponseMessage> FetchChaos(string chaosUrl);
    }
}
