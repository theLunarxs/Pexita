﻿using Pexita.Data.Entities.Tags;
using Pexita.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NunitTest.FakeServices
{
    internal class FakeTagsService : ITagsService
    {
        public bool AddTag(TagCreateVM product)
        {
            throw new NotImplementedException();
        }

        public bool DeleteTag(int id)
        {
            throw new NotImplementedException();
        }

        public List<TagInfoVM> GetAllTags()
        {
            throw new NotImplementedException();
        }

        public TagInfoVM GetTagByID(int id)
        {
            throw new NotImplementedException();
        }

        public List<TagInfoVM> TagsToVM(List<TagModel> tags)
        {
            throw new NotImplementedException();
        }

        public TagInfoVM UpdateTag(int id, TagCreateVM product)
        {
            throw new NotImplementedException();
        }
    }
}