// CGAL_StraightSkeleton_Wrapper.cpp : Defines the exported functions for the DLL application.
//

#include "stdafx.h"
#include<vector>
#include<boost/shared_ptr.hpp>
#include<CGAL/Exact_predicates_inexact_constructions_kernel.h>
#include<CGAL/Polygon_with_holes_2.h>
#include<CGAL/create_straight_skeleton_from_polygon_with_holes_2.h>
#include<CGAL/Polygon_2.h>
#include<CGAL/create_offset_polygons_2.h>

typedef CGAL::Exact_predicates_inexact_constructions_kernel K;

typedef K::Point_2                    Point;
typedef CGAL::Polygon_2<K>            Polygon_2;
typedef CGAL::Polygon_with_holes_2<K> Polygon_with_holes;
typedef CGAL::Straight_skeleton_2<K>  Ss;

typedef boost::shared_ptr<Ss> SsPtr;

typedef Ss::Vertex_const_handle		Vertex_const_handle;
typedef Ss::Halfedge_const_handle	Halfedge_const_handle;
typedef Ss::Halfedge_const_iterator	Halfedge_const_iterator;

typedef boost::shared_ptr<Polygon_2> PolygonPtr;
typedef std::vector<PolygonPtr> PolygonPtrVector;

struct Poly
{
	float* vertices;
	int verticesCount;
};

struct PolyArray
{
	int length;
	Poly* start;
};

struct SkeletonHandle
{
	SsPtr Skeleton;

	SkeletonHandle(SsPtr ptr)
	{
		Skeleton = ptr;
	}
};

Polygon_2 CreatePolygon(float* vertices, int length)
{
	Polygon_2 result;
	for (int i = 0; i < length; i++)
	{
		//Get x and y elements
		float x = vertices[i * 2];
		float y = vertices[i * 2 + 1];

		//Create point
		result.push_back(Point(x, y));
	}

	return result;
}

extern "C" __declspec(dllexport) void* GenerateStraightSkeleton(Poly* outer, Poly* holes, int holesCount, Poly* outStraightSkeleton)
{
	//Construct outer polygon (no holes yet)
	Polygon_with_holes poly(CreatePolygon(outer->vertices, outer->verticesCount));

	//Add holes
	for (int h = 0; h < holesCount; h++)
		poly.add_hole(CreatePolygon(holes[h].vertices, holes[h].verticesCount));

	//Construct skeleton
	SsPtr iss = CGAL::create_interior_straight_skeleton_2(poly);
	Ss& ss = *iss;

	//Create result
	outStraightSkeleton->verticesCount = ss.size_of_halfedges() * 2 * 2;	// edges * 2 (start and end) * 2 (X and Y)
	outStraightSkeleton->vertices = new float[outStraightSkeleton->verticesCount];

	//Copy vertex pairs (also measure the longest edge while we're at it)
	float longest = 0;
	int index = 0;
	for (Halfedge_const_iterator i = ss.halfedges_begin(); i != ss.halfedges_end(); ++i)
	{
		auto start = i->vertex()->point();
		auto end = i->next()->vertex()->point();

		outStraightSkeleton->vertices[index * 4] = start.x();
		outStraightSkeleton->vertices[index * 4 + 1] = start.y();
		outStraightSkeleton->vertices[index * 4 + 2] = end.x();
		outStraightSkeleton->vertices[index * 4 + 3] = end.y();

		longest = fmaxf(sqrtf(powf(end.x() - start.x() + end.y() - start.y(), 2)), longest);

		index++;
	}

	//Create a handle for the result (effectively "leak" the skeleton out to C#)
	auto handle = new SkeletonHandle(iss);
	return (void*)handle;
}

extern "C" __declspec(dllexport) PolyArray GenerateOffsetPolygon(void* opaqueHandle, float distance)
{
	SkeletonHandle* handle = (SkeletonHandle*)opaqueHandle;
	Ss& ss = *(handle->Skeleton);

	PolygonPtrVector offset_polygons = CGAL::create_offset_polygons_2<Polygon_2>(distance, ss);

	PolyArray result;
	result.length = offset_polygons.size();
	result.start = new Poly[offset_polygons.size()];

	for (int i = 0; i < offset_polygons.size(); i++)
	{
		//Get some handy pointers (copy from/copy to)
		PolygonPtr polygon = offset_polygons[i];
		Poly* resultPoly = &result.start[i];

		//Create the destination to put the results into
		resultPoly->verticesCount = polygon->size();
		resultPoly->vertices = new float[resultPoly->verticesCount * 2];

		//Copy the vertices
		for (int v = 0; v < polygon->size(); v++)
		{
			Point point = polygon->vertex(v);
			resultPoly->vertices[v * 2] = point.x();
			resultPoly->vertices[v * 2 + 1] = point.y();
		}
	}

	return result;
}

extern "C" __declspec(dllexport) void FreePolygonStructMembers(Poly* poly)
{
	if (poly != nullptr)
	{
		delete poly->vertices;

		poly->vertices = nullptr;
		poly->verticesCount = 0;
	}
}

extern "C" __declspec(dllexport) void FreeResultHandle(void* opaqueHandle)
{
	if (opaqueHandle != nullptr)
	{
		SkeletonHandle* handle = (SkeletonHandle*)opaqueHandle;
		delete handle;
	}
}

extern "C" __declspec(dllexport) void FreePolyArray(PolyArray handle)
{
	if (handle.start != nullptr)
	{
		for (int i = 0; i < handle.length; i++)
			FreePolygonStructMembers(&handle.start[i]);

		delete handle.start;
	}
}