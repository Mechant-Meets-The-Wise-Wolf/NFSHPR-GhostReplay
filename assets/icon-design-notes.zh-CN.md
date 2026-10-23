# HPRGhostReplay 图标制作记录

工具：内置 imagegen 图像编辑；后续使用系统 System.Drawing 进行尺寸转换和 ICO 容器封装，无终端用户运行库依赖。

参考：本机 Steam 的原作红白图标（appid 1328660）；游戏 EXE 中另一套图标仅供高分辨率轮廓参考。用户否定了重新设计为 R 的方案，并指出白色底板会遮挡虚影。最终方向为仅保留原红色标志，移除两层白色底板，使用错开的透明红色副本。

原标志属于相关权利人，不宣称为本工具原创标识；相关说明保留在 THIRD-PARTY-NOTICES.txt。

## 最终编辑提示词（原文）

Precise icon edit. USER CORRECTION: WHITE BACKGROUND BLOCKS THE GHOST. REMOVE ALL WHITE TILES AND ALL WHITE BACKGROUND from both icons. Output ONLY TWO original red NFS angular symbols on an entirely transparent alpha canvas. Image 1 is original logo identity. Image 2 shows the intended original red glyph silhouette but its white square cards are WRONG and must be completely removed. Keep the original NFS glyph exactly recognizable, not an R or a new monogram. Composition: the foreground original red NFS symbol is fully opaque strong red (#EE2337), at lower-left; ONE duplicate of the SAME RED SYMBOL sits behind it shifted toward upper-right and parallel with ZERO rotation, opacity about 25 percent, enough offset that most of the rear symbol is visible outside the solid red foreground. The foreground red shape can occlude the ghost ONLY where red strokes overlap; every interior gap between the foreground's red strokes must remain truly transparent so the rear symbol can show through. No tile, no white material, no white rectangle, no panel, no rounded square, no border, no drop shadow, no glow, no gradients, no texture, no checkerboard, no text. Just two clean flat red vector-like logo silhouettes, solid main logo plus faint red ghost. Square canvas with 6 percent transparent padding around the combined symbol bounds. Exact original slanted racing glyph identity is critical. This is a transparent PNG asset, not a product mockup.


## 最终布局调整提示词（原文）

Create a clean transparent Windows icon based on the original NFS mark in these references. Exactly TWO copies of the NFS glyph total, never three. NO square cards or backgrounds. Copy the original glyph shape faithfully; use bright red instead of the white seen in reference 2, reference 1 shows the desired red. Simple layout on a square canvas: the FULLY OPAQUE original red mark in the upper-left, spanning approximately x=6% to 84%, y=10% to 58%; one IDENTICAL red mark behind it shifted DOWN AND RIGHT, spanning x=16% to 94%, y=42% to 90%, at only 25% opacity. Thus there are two distinct parallel red marks, the faint ghost is below the solid original. The two marks must have exactly the same shape and size. Keep a clear readable gap between the broad upper strokes so the layers are distinct, with limited stroke overlap. Entire canvas is transparent alpha outside the red shapes, including all openings inside the marks. No white, no black, no tiles, no rounded squares, no borders, no perspective, no gradients, no texture, no speckles, no blur, no shadow, no motion streaks, no extra marks, no letters, no R, no text. TWO clean flat NFS symbols ONLY, one solid upper left and one faint lower right.
