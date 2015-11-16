## CGAL_StraightSkeleton_Wrapper

A C# wrapper around [CGAL](http://www.cgal.org/) for calculating [straight skeletons](https://en.wikipedia.org/wiki/Straight_skeleton) of shapes. This is *not* and does not ever intend to be a general C# wrapper around CGAL! 

![Skeleton](Skeleton.png)

## Building

This was built for windows, cross platform building would be nice if someone wants to make a PR!

 1. Install [CGAL dependencies](http://www.cgal.org/download/windows.html#PrerequisitesforBuildingthe32-bitCGALLibraryusingMicrosoftVisualStudio)
    - This largely consists of installing Boost, then building CGAL
    - Build process depends on using the correct compiler, that's v12 (VS2013)
    - Dependencies must be in correct locations:
      - ```C:\local\boost_1_59_0```
      - ```C:\Program Files\CGAL-4.7```
 2. Once you have CGAL built you can build the CGAL_StraightSkeleton_Wrapper project (check project properties for paths in linker, just to make sure you have everything right).
    - I hope you enjoy solving dependency problems!

 3. Now you can build CGAL_StraightSkeleton_Dotnet and use it! See ConsoleTest project for a demo on how to use it.