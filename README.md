#Redis Session State Provider#
Redis is an open source, advanced key-value store. It is often referred to as a data structure server since keys can contain strings, hashes, lists, sets and sorted sets.
It is the perfect solution for storing session level cache objects for ASP.NET. Redis has its own built in expiration mechanisms for cleaning up old keys so no
new functionality will have to be built for that.

To use the RedisSessionStateProvider, modify the sessionState section in the web.config. Below is an example. 

    <sessionState mode="Custom" customProvider="RedisSessionStateProvider" cookieless="false" timeout="1">
      <providers>
        <add name="RedisSessionStateProvider" 
             type="RedisProvider.SessionProvider.CustomServiceProvider"
             server="localhost"
             port="6379" 
             password="" 
             writeExceptionsToEventLog="false" />
      </providers>
    </sessionState>

