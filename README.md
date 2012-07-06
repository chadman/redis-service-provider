#Redis Session State Provider#
Redis is an open source, advanced key-value store. It is often referred to as a data structure server since keys can contain strings, hashes, lists, sets and sorted sets.
It is the perfect solution for storing session level cache objects for ASP.NET. Redis has its own built in expiration mechanisms for cleaning up old keys so no
new functionality will have to be built for that.

The session provider can be found under RedisProvider < SessionProvider < RedisSessionProvider.cs

I wrote a blog post to explain the implementation, please check it out here: http://wp.me/pPMqN-w
