﻿using System.Reactive.Linq;
using Cleipnir.ResilientFunctions.Domain;
using Cleipnir.ResilientFunctions.Messaging.Core;
using Cleipnir.ResilientFunctions.Messaging.SamplesConsoleApp.JobScheduler.Domain;
using Cleipnir.ResilientFunctions.Messaging.SamplesConsoleApp.JobScheduler.ExternalEntities;

namespace Cleipnir.ResilientFunctions.Messaging.SamplesConsoleApp.JobScheduler.Saga;

public class CoordinatorSaga
{
    private readonly IEventStore _eventStore;
    private readonly MessageQueue _messageQueue;
    private readonly int _numberOfWorkers;
    
    public const string FunctionTypeId = "JobScheduler";
    
    public CoordinatorSaga(
        RFunctions rFunctions, 
        IEventStore eventStore, 
        MessageQueue messageQueue,
        int numberOfWorkers)
    {
        _eventStore = eventStore;
        _messageQueue = messageQueue;
        _numberOfWorkers = numberOfWorkers;

        messageQueue.Subscribers += msg =>
        {
            switch (msg)
            {
                case JobAccepted j:
                    _eventStore
                        .GetEventSource(FunctionTypeId, j.JobId.ToString())
                        .ContinueWith(esTask => esTask.Result.Emit(j, $"{nameof(JobAccepted)}_{j.WorkerId}"));
                    return;
                case JobRefused j:
                    _eventStore
                        .GetEventSource(FunctionTypeId, j.JobId.ToString())
                        .ContinueWith(esTask => esTask.Result.Emit(j, $"{nameof(JobRefused)}_{j.WorkerId}"));
                    return;
                case JobCompleted j:
                    _eventStore
                        .GetEventSource(FunctionTypeId, j.JobId.ToString())
                        .ContinueWith(esTask => esTask.Result.Emit(j, $"{nameof(JobCompleted)}_{j.WorkerId}"));
                    return;
            }
        };

        ScheduleJob = rFunctions.RegisterAction<Guid, Scrapbook>(
            FunctionTypeId,
            _ScheduleJob
        ).Invoke;
    }

    public RAction.Invoke<Guid> ScheduleJob { get; }

    private async Task _ScheduleJob(Guid jobId, Scrapbook scrapbook)
    {
        using var eventSource = await _eventStore
            .GetEventSource(new FunctionId(FunctionTypeId, jobId.ToString()));

        CancelJobAndThrowIfFailedSchedulation(jobId, scrapbook);
        
        await SendJobReservation(jobId, scrapbook);

        if (!scrapbook.JobOrderSent)
        {
            var acceptedJob = await AwaitReplies(jobId, eventSource, scrapbook);
            await SendJobOrder(jobId, acceptedJob.WorkerId, scrapbook);    
        }
        
        await AwaitJobCompletion(eventSource);
    }

    private void CancelJobAndThrowIfFailedSchedulation(Guid jobId, Scrapbook scrapbook)
    {
        if (!scrapbook.SchedulingFailed) return;

        _messageQueue.Send(new JobCancellation(jobId));
        throw new Exception("Job schedulation failed");
    }

    private async Task SendJobReservation(Guid jobId, Scrapbook scrapbook)
    {
        if (scrapbook.JobReservationSent) return;

        scrapbook.JobReservationSent = true;
        await scrapbook.Save();
        
        //send job reservation request
        _messageQueue.Send(new JobReservation(jobId));
    }

    private async Task<JobAccepted> AwaitReplies(Guid jobId, EventSource eventSource, Scrapbook scrapbook)
    {
        //wait for job reservation replies
        var acceptedJob = eventSource
            .All
            .OfType<JobAccepted>()
            .NextEvent();

        var jobRefusals = eventSource
            .All
            .OfType<JobRefused>()
            .Take(_numberOfWorkers)
            .LastAsync()
            .NextEvent();

        var timeout = Task.Delay(2_000);

        await Task.WhenAny(acceptedJob, jobRefusals, timeout);

        if (acceptedJob.IsCompleted)
            return acceptedJob.Result;
        
        scrapbook.SchedulingFailed = true;
        await scrapbook.Save();
        _messageQueue.Send(new JobCancellation(jobId));
        throw new Exception("Job schedulation failed");
    }

    private async Task SendJobOrder(Guid jobId, Guid workerId, Scrapbook scrapbook)
    {
        scrapbook.JobReservationSent = true;
        await scrapbook.Save();
        _messageQueue.Send(new JobOrder(jobId, workerId));
    }

    private async Task AwaitJobCompletion(EventSource eventSource) 
        => await eventSource.All.OfType<JobCompleted>().NextEvent(10_000);

    private class Scrapbook : RScrapbook
    {
        public bool JobReservationSent { get; set; }
        public bool JobOrderSent { get; set; }
        public bool SchedulingFailed { get; set; }
    }
}