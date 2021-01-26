﻿using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Core;
using Umbraco.Core.Services;
using Umbraco.Web.Common.Attributes;
using Umbraco.Web.Common.Authorization;
using Umbraco.Web.Models.Trees;
using Umbraco.Web.Trees;
using Umbraco.Web.WebApi;

namespace Umbraco.Web.BackOffice.Trees
{
    [Authorize(Policy = AuthorizationPolicies.TreeAccessMemberGroups)]
    [Tree(Constants.Applications.Members, Constants.Trees.MemberGroups, SortOrder = 1)]
    [PluginController(Constants.Web.Mvc.BackOfficeTreeArea)]
    [CoreTree]
    public class MemberGroupTreeController : MemberTypeAndGroupTreeControllerBase
    {
        private readonly IMemberGroupService _memberGroupService;

        public MemberGroupTreeController(
            ILocalizedTextService localizedTextService,
            UmbracoApiControllerTypeCollection umbracoApiControllerTypeCollection,
            IMenuItemCollectionFactory menuItemCollectionFactory,
            IMemberGroupService memberGroupService)
            : base(localizedTextService, umbracoApiControllerTypeCollection, menuItemCollectionFactory)
        {
            _memberGroupService = memberGroupService;
        }

        protected override IEnumerable<TreeNode> GetTreeNodesFromService(string id, FormCollection queryStrings)
        {
            return _memberGroupService.GetAll()
                .OrderBy(x => x.Name)
                .Select(dt => CreateTreeNode(dt.Id.ToString(), id, queryStrings, dt.Name, Constants.Icons.MemberGroup, false));
        }

        protected override ActionResult<TreeNode> CreateRootNode(FormCollection queryStrings)
        {
            var rootResult = base.CreateRootNode(queryStrings);
            if (!(rootResult.Result is null))
            {
                return rootResult;
            }
            var root = rootResult.Value;

            //check if there are any groups
            root.HasChildren = _memberGroupService.GetAll().Any();
            return root;
        }
    }
}