﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using ModelBindingWebSite.Models;

namespace ModelBindingWebSite.Services
{
    public interface IVehicleService
    {
        void Update(int id, VehicleViewModel vehicle, string trackingId);
    }
}