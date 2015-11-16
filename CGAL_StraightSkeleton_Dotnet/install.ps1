param($installPath, $toolsPath, $package, $project)

Function MakeCopyToOutput($proj, $name)
{
    $item = $proj.ProjectItems.Item($name);
    $item.Properties.Item("BuildAction").Value = [int]0
    $item.Properties.Item("CopyToOutputDirectory").Value = [int]2
}

MakeCopyToOutput($project, "boost_chrono-vc120-mt-gd-1_59.dll");
MakeCopyToOutput($project, "boost_system-vc120-mt-gd-1_59.dll");
MakeCopyToOutput($project, "boost_thread-vc120-mt-gd-1_59.dll");
MakeCopyToOutput($project, "CGAL_Core-vc120-mt-gd-4.7.dll");
MakeCopyToOutput($project, "CGAL-vc120-mt-gd-4.7.dll");
MakeCopyToOutput($project, "libgmp-10.dll");
MakeCopyToOutput($project, "libmpfr-4.dll");
MakeCopyToOutput($project, "CGAL_StraightSkeleton_Wrapper.dll");