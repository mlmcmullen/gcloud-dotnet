﻿// Copyright 2016 Google Inc. All Rights Reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Google.Api.Gax.Rest;
using Google.Apis.Bigquery.v2.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using static Google.Apis.Bigquery.v2.JobsResource;

namespace Google.Bigquery.V2
{
    public partial class BigqueryClientImpl
    {
        private sealed class JobPageManager : IPageManager<ListRequest, JobList, BigqueryJob>
        {
            private readonly BigqueryClient _client;

            internal JobPageManager(BigqueryClient client)
            {
                _client = client;
            }

            public string GetNextPageToken(JobList response) => response.NextPageToken;
            public IEnumerable<BigqueryJob> GetResources(JobList response) => response.Jobs?.Select(resource => new BigqueryJob(_client, resource));
            public void SetPageSize(ListRequest request, int pageSize) => request.MaxResults = pageSize;
            public void SetPageToken(ListRequest request, string pageToken) => request.PageToken = pageToken;
        }

        /// <inheritdoc />
        public override IPagedEnumerable<JobList, BigqueryJob> ListJobs(ProjectReference projectReference, ListJobsOptions options = null)
        {
            GaxRestPreconditions.CheckNotNull(projectReference, nameof(projectReference));

            var pageManager = new JobPageManager(this);
            return new PagedEnumerable<ListRequest, JobList, BigqueryJob>(
                () => CreateListJobsRequest(projectReference, options),
                pageManager);
        }

        private ListRequest CreateListJobsRequest(ProjectReference projectReference, ListJobsOptions options = null)
        {
            var request = Service.Jobs.List(projectReference.ProjectId);
            options?.ModifyRequest(request);
            return request;
        }

        /// <inheritdoc />
        public override BigqueryJob PollJob(JobReference jobReference, PollJobOptions options = null)
        {
            GaxRestPreconditions.CheckNotNull(jobReference, nameof(jobReference));
            options?.Validate();

            DateTimeOffset? deadline = options?.GetEffectiveDeadline() ?? DateTimeOffset.MaxValue;
            long maxRequests = options?.MaxRequests ?? long.MaxValue;
            TimeSpan interval = options?.Interval ?? TimeSpan.FromSeconds(1);

            for (long i = 0; i < maxRequests && DateTimeOffset.UtcNow < deadline; i++)
            {
                var job = GetJob(jobReference);
                if (job.State == JobState.Done)
                {
                    return job;
                }
                Thread.Sleep(interval);
            }
            throw new TimeoutException($"Job {jobReference.JobId} did not complete in time.");
        }

        /// <inheritdoc />
        public override BigqueryJob GetJob(JobReference jobReference, GetJobOptions options = null)
        {
            GaxRestPreconditions.CheckNotNull(jobReference, nameof(jobReference));

            var request = Service.Jobs.Get(jobReference.ProjectId, jobReference.JobId);
            options?.ModifyRequest(request);
            var job = request.Execute();
            return new BigqueryJob(this, job);
        }

        /// <inheritdoc />
        public override BigqueryJob CancelJob(JobReference jobReference, CancelJobOptions options = null)
        {
            GaxRestPreconditions.CheckNotNull(jobReference, nameof(jobReference));
            var request = Service.Jobs.Cancel(jobReference.ProjectId, jobReference.JobId);
            options?.ModifyRequest(request);
            var job = request.Execute().Job;
            return new BigqueryJob(this, job);
        }
    }
}
