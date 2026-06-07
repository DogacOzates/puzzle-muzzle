#import <UIKit/UIKit.h>

extern "C" {
    void _ShowShareSheet(const char* text) {
        NSString* shareText = [NSString stringWithUTF8String:text];
        UIActivityViewController* vc = [[UIActivityViewController alloc]
            initWithActivityItems:@[shareText] applicationActivities:nil];

        // iOS 13+ compatible root view controller lookup
        UIWindowScene* scene = nil;
        for (UIScene* s in [UIApplication sharedApplication].connectedScenes) {
            if ([s isKindOfClass:[UIWindowScene class]] && s.activationState == UISceneActivationStateForegroundActive) {
                scene = (UIWindowScene*)s;
                break;
            }
        }
        UIViewController* root = scene.windows.firstObject.rootViewController;
        if (!root) root = [UIApplication sharedApplication].windows.firstObject.rootViewController;

        // Walk to the top-most presented controller
        while (root.presentedViewController) root = root.presentedViewController;

        if (UI_USER_INTERFACE_IDIOM() == UIUserInterfaceIdiomPad) {
            vc.popoverPresentationController.sourceView = root.view;
            vc.popoverPresentationController.sourceRect =
                CGRectMake(root.view.bounds.size.width / 2, root.view.bounds.size.height / 2, 0, 0);
        }
        [root presentViewController:vc animated:YES completion:nil];
    }
}
