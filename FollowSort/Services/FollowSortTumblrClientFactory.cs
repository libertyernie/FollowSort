using DontPanic.TumblrSharp;
using DontPanic.TumblrSharp.Client;
using DontPanic.TumblrSharp.OAuth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FollowSort.Services
{
    public class FollowSortTumblrClientFactory
    {
        private readonly TumblrClientFactory _factory = new TumblrClientFactory();
        private readonly string _consumerKey, _consumerSecret;

        public FollowSortTumblrClientFactory(string consumerKey, string consumerSecret)
        {
            _consumerKey = consumerKey;
            _consumerSecret = consumerSecret;
        }
        
        public TumblrClient Create(Token oAuthToken = null)
        {
            return _factory.Create<TumblrClient>(_consumerKey, _consumerSecret, oAuthToken);
        }

        public TumblrClient Create(string consumerKey, string consumerSecret)
        {
            return _factory.Create<TumblrClient>(_consumerKey, _consumerSecret, new Token(consumerKey, consumerSecret));
        }
    }
}
