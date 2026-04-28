#import <Foundation/Foundation.h>
#import <Security/Security.h>

static NSString* const kService = @"com.dogac.puzzlemuzzle";

static void KeychainSet(NSString* key, BOOL value) {
    NSData* data = [NSData dataWithBytes:&value length:sizeof(BOOL)];
    NSDictionary* query = @{
        (__bridge id)kSecClass:           (__bridge id)kSecClassGenericPassword,
        (__bridge id)kSecAttrService:     kService,
        (__bridge id)kSecAttrAccount:     key,
    };
    SecItemDelete((__bridge CFDictionaryRef)query);
    NSDictionary* item = @{
        (__bridge id)kSecClass:           (__bridge id)kSecClassGenericPassword,
        (__bridge id)kSecAttrService:     kService,
        (__bridge id)kSecAttrAccount:     key,
        (__bridge id)kSecValueData:       data,
        (__bridge id)kSecAttrAccessible:  (__bridge id)kSecAttrAccessibleAfterFirstUnlock,
    };
    SecItemAdd((__bridge CFDictionaryRef)item, NULL);
}

static BOOL KeychainGet(NSString* key) {
    NSDictionary* query = @{
        (__bridge id)kSecClass:       (__bridge id)kSecClassGenericPassword,
        (__bridge id)kSecAttrService: kService,
        (__bridge id)kSecAttrAccount: key,
        (__bridge id)kSecReturnData:  @YES,
        (__bridge id)kSecMatchLimit:  (__bridge id)kSecMatchLimitOne,
    };
    CFDataRef result = NULL;
    OSStatus status = SecItemCopyMatching((__bridge CFDictionaryRef)query, (CFTypeRef*)&result);
    if (status == errSecSuccess && result != NULL) {
        NSData* data = (__bridge_transfer NSData*)result;
        BOOL value = NO;
        if (data.length >= sizeof(BOOL))
            [data getBytes:&value length:sizeof(BOOL)];
        return value;
    }
    return NO;
}

extern "C" {
    bool _KeychainGetBool(const char* key) {
        return KeychainGet([NSString stringWithUTF8String:key]);
    }
    void _KeychainSetBool(const char* key, bool value) {
        KeychainSet([NSString stringWithUTF8String:key], (BOOL)value);
    }
}
