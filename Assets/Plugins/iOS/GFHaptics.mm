// Vibrations courtes (Taptic Engine) pour Gold Foundry — appelées depuis GFHaptics.cs
#import <UIKit/UIKit.h>

extern "C" void GFHapticImpact(int style)
{
    UIImpactFeedbackStyle s = UIImpactFeedbackStyleLight;
    if (style == 1) s = UIImpactFeedbackStyleMedium;
    else if (style >= 2) s = UIImpactFeedbackStyleHeavy;
    UIImpactFeedbackGenerator *g = [[UIImpactFeedbackGenerator alloc] initWithStyle:s];
    [g prepare];
    [g impactOccurred];
}
