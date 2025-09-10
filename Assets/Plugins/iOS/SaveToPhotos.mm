//
//  SaveToPhotos.m
//  Toukai-C
//
//  Created by 誠太都築 on 2025/09/10.
//

#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
extern "C" void SaveJPGToCameraRoll(const void* data, int length) {
    @autoreleasepool {
        if (!data || length <= 0) return;
        NSData* nsdata = [NSData dataWithBytes:data length:length];
        UIImage* image = [UIImage imageWithData:nsdata];
        if (!image) return;
        UIImageWriteToSavedPhotosAlbum(image, nil, nil, nil);
    }
}
