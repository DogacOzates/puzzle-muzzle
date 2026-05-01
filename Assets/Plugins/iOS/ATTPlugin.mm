#import <AppTrackingTransparency/AppTrackingTransparency.h>
#import <Foundation/Foundation.h>

typedef void (*ATTCallbackFn)(int status);

extern "C" {
    void _RequestATT(ATTCallbackFn callback) {
        if (@available(iOS 14, *)) {
            ATTrackingManagerAuthorizationStatus current =
                ATTrackingManager.trackingAuthorizationStatus;
            // If already determined (e.g. re-launch), skip dialog and return immediately
            if (current != ATTrackingManagerAuthorizationStatusNotDetermined) {
                if (callback) callback((int)current);
                return;
            }
            [ATTrackingManager requestTrackingAuthorizationWithCompletionHandler:
                ^(ATTrackingManagerAuthorizationStatus status) {
                    dispatch_async(dispatch_get_main_queue(), ^{
                        if (callback) callback((int)status);
                    });
                }
            ];
        } else {
            // iOS < 14: tracking always authorized
            if (callback) callback(3);
        }
    }
}
