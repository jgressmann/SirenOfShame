﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TeamFoundation;
using Microsoft.TeamFoundation.Build.Client;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Framework.Client;
using Microsoft.TeamFoundation.Server;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.VersionControl.Client;
using SirenOfShame.Lib;
using log4net;

namespace TfsServices.Configuration
{
    public class MyTfsProjectCollection
    {
        private static readonly ILog _log = MyLogManager.GetLogger(typeof(MyTfsProjectCollection));
        private readonly ICommonStructureService _commonStructureService;
        private readonly TfsTeamProjectCollection _tfsTeamProjectCollection;
        private readonly IBuildServer _buildServer;
        private readonly TswaClientHyperlinkService _tswaClientHyperlinkService;
        private MyTfsServer _myTfsServer;
        public bool CurrentUserHasAccess { get; set; }

        public MyTfsServer MyTfsServer
        {
            get { return _myTfsServer; }
        }

        public MyTfsProjectCollection(MyTfsServer myTfsServer, CatalogNode teamProjectCollectionNode)
        {
            try
            {
                _myTfsServer = myTfsServer;
                Name = teamProjectCollectionNode.Resource.DisplayName;
                ServiceDefinition tpcServiceDefinition = teamProjectCollectionNode.Resource.ServiceReferences["Location"];
                var configLocationService = myTfsServer.GetConfigLocationService();
                var tpcUri = new Uri(configLocationService.LocationForCurrentConnection(tpcServiceDefinition));
                _tfsTeamProjectCollection = TfsTeamProjectCollectionFactory.GetTeamProjectCollection(tpcUri);
                _commonStructureService = _tfsTeamProjectCollection.GetService<ICommonStructureService>();
                _buildServer = _tfsTeamProjectCollection.GetService<IBuildServer>();
                _tswaClientHyperlinkService = _tfsTeamProjectCollection.GetService<TswaClientHyperlinkService>();
                CurrentUserHasAccess = true;
            }
            catch (TeamFoundationServiceUnavailableException ex)
            {
                _log.Debug("Can't access " + Name + ". This could be because the project collection is currently offline.", ex);
                CurrentUserHasAccess = false;
            }
            catch (TeamFoundationServerUnauthorizedException ex)
            {
                _log.Debug("Unauthorized access to " + teamProjectCollectionNode, ex);
                CurrentUserHasAccess = false;
            }
        }

        public string ConvertTfsUriToUrl(Uri vstfsUri)
        {
            if (vstfsUri == null) return null;
            if (_tswaClientHyperlinkService == null) return null;
            var uri = _tswaClientHyperlinkService.GetViewBuildDetailsUrl(vstfsUri);
            if (uri == null) return null;
            return uri.ToString();
        }
        
        public IEnumerable<MyTfsProject> Projects
        {
            get
            {
                try
                {
                    return _commonStructureService.ListAllProjects().Select(p => new MyTfsProject(p, _buildServer, this));
                } 
                catch (TeamFoundationServiceUnavailableException)
                {
                    _log.Debug("Retrieving projects from " + Name + " resulted in TeamFoundationServiceUnavailableException. This could be because the project collection is currently offline.");
                    return Enumerable.Empty<MyTfsProject>();
                }
            }
        }

        public string Name { get; set; }

        public VersionControlServer VersionControlServer
        {
            get { return _tfsTeamProjectCollection.GetService<VersionControlServer>(); }
        }

        public Uri Uri
        {
            get { return _tfsTeamProjectCollection.Uri; }
        }

        public GitHttpClient GetGitHttpClient()
        {
            var vssCredentials = MyTfsServer.GetVssCredentials();
            var projectCollectionUri = Uri;
            GitHttpClient client = new GitHttpClient(projectCollectionUri, vssCredentials);
            return client;
        }
    }
}
