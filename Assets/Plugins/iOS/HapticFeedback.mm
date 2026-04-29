#import <UIKit/UIKit.h>

extern "C" {
    void _HapticLight() {
        if (@available(iOS 10.0, *)) {
            UIImpactFeedbackGenerator *gen =
                [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight];
            [gen prepare];
            [gen impactOccurred];
        }
    }

    void _HapticSuccess() {
        if (@available(iOS 10.0, *)) {
            UINotificationFeedbackGenerator *gen =
                [[UINotificationFeedbackGenerator alloc] init];
            [gen prepare];
            [gen notificationOccurred:UINotificationFeedbackTypeSuccess];
        }
    }
}
