﻿using System;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;

using Abp.Dependency;
using Abp.Runtime.Caching;

using DynamicTranslator.Application.Orchestrators.Detectors;
using DynamicTranslator.Application.Orchestrators.Finders;
using DynamicTranslator.Application.Orchestrators.Organizers;
using DynamicTranslator.Application.Requests;
using DynamicTranslator.Configuration.Startup;
using DynamicTranslator.Constants;
using DynamicTranslator.Domain.Events;
using DynamicTranslator.Domain.Model;
using DynamicTranslator.Service.GoogleAnalytics;
using DynamicTranslator.Wpf.Notification;

namespace DynamicTranslator.Wpf.Observers
{
    public class Finder : IObserver<EventPattern<WhenClipboardContainsTextEventArgs>>, ISingletonDependency
    {
        private readonly ICacheManager _cacheManager;
        private readonly IDynamicTranslatorConfiguration _configuration;
        private readonly IGoogleAnalyticsService _googleAnalytics;
        private readonly ILanguageDetector _languageDetector;
        private readonly IMeanFinderFactory _meanFinderFactory;
        private readonly INotifier _notifier;
        private readonly IResultOrganizer _resultOrganizer;
        private string _previousString;

        public Finder(INotifier notifier,
            IMeanFinderFactory meanFinderFactory,
            IResultOrganizer resultOrganizer,
            ICacheManager cacheManager,
            IGoogleAnalyticsService googleAnalytics,
            ILanguageDetector languageDetector,
            IDynamicTranslatorConfiguration configuration)
        {
            _notifier = notifier;
            _meanFinderFactory = meanFinderFactory;
            _resultOrganizer = resultOrganizer;
            _cacheManager = cacheManager;
            _googleAnalytics = googleAnalytics;
            _languageDetector = languageDetector;
            _configuration = configuration;
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(EventPattern<WhenClipboardContainsTextEventArgs> value)
        {
            Task.Run(async () =>
            {
                try
                {
                    string currentString = value.EventArgs.CurrentString;

                    if (_previousString == currentString)
                    {
                        return;
                    }

                    _previousString = currentString;
                    Maybe<string> failedResults;

                    string fromLanguageExtension = await _languageDetector.DetectLanguage(currentString);
                    TranslateResult[] results = await GetMeansFromCache(currentString, fromLanguageExtension);
                    Maybe<string> findedMeans = await _resultOrganizer.OrganizeResult(results, currentString, out failedResults).ConfigureAwait(false);

                    await Notify(currentString, findedMeans);
                    await Notify(currentString, failedResults);
                    await Trace(currentString, fromLanguageExtension);
                }
                catch (Exception ex)
                {
                    await Notify("Error", new Maybe<string>(ex.Message));
                }
            });
        }

        private async Task Trace(string currentString, string fromLanguageExtension)
        {
            await _googleAnalytics.TrackEventAsync("DynamicTranslator",
                "Translate",
                $"{currentString} | {fromLanguageExtension} - {_configuration.ApplicationConfiguration.ToLanguage.Extension} | v{ApplicationVersion.GetCurrentVersion()} ",
                null).ConfigureAwait(false);

            await _googleAnalytics.TrackAppScreenAsync("DynamicTranslator",
                ApplicationVersion.GetCurrentVersion(),
                "dynamictranslator",
                "dynamictranslator",
                "notification").ConfigureAwait(false);
        }

        private async Task Notify(string currentString, Maybe<string> findedMeans)
        {
            if (!string.IsNullOrEmpty(findedMeans.DefaultIfEmpty(string.Empty).First()))
            {
                await _notifier.AddNotificationAsync(currentString,
                    ImageUrls.NotificationUrl,
                    findedMeans.DefaultIfEmpty(string.Empty).First()
                );
            }
        }

        private Task<TranslateResult[]> GetMeansFromCache(string currentString, string fromLanguageExtension)
        {
            Task<TranslateResult[]> meanTasks = Task.WhenAll(_meanFinderFactory.GetFinders().Select(t => t.FindMean(new TranslateRequest(currentString, fromLanguageExtension))));

            return _cacheManager.GetCache<string, TranslateResult[]>(CacheNames.MeanCache)
                                .GetAsync(currentString, () => meanTasks);
        }
    }
}
