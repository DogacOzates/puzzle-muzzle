#include <Foundation/Foundation.h>

// Stored to avoid duplicate observer registration across multiple _iCloudStartSync calls.
static id s_iCloudObserver = nil;

extern "C"
{
    void _iCloudSetInt(const char* key, int value)
    {
        NSString* nsKey = [NSString stringWithUTF8String:key];
        [[NSUbiquitousKeyValueStore defaultStore] setLongLong:value forKey:nsKey];
        [[NSUbiquitousKeyValueStore defaultStore] synchronize];
    }

    int _iCloudGetInt(const char* key, int defaultValue)
    {
        NSString* nsKey = [NSString stringWithUTF8String:key];
        NSUbiquitousKeyValueStore* store = [NSUbiquitousKeyValueStore defaultStore];
        id obj = [store objectForKey:nsKey];
        if (obj == nil) return defaultValue;
        return (int)[store longLongForKey:nsKey];
    }

    void _iCloudStartSync()
    {
        [[NSUbiquitousKeyValueStore defaultStore] synchronize];

        // Avoid registering duplicate observers on repeated calls.
        if (s_iCloudObserver != nil) return;

        s_iCloudObserver = [[NSNotificationCenter defaultCenter]
            addObserverForName:NSUbiquitousKeyValueStoreDidChangeExternallyNotification
            object:[NSUbiquitousKeyValueStore defaultStore]
            queue:nil
            usingBlock:^(NSNotification* notification) {
                // Always deliver to Unity on the main thread.
                dispatch_async(dispatch_get_main_queue(), ^{
                    UnitySendMessage("iCloudSyncManager", "OnExternalChange", "");
                });
            }];
    }
}
