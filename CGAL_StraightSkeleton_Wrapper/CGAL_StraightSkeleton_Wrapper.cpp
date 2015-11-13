// CGAL_StraightSkeleton_Wrapper.cpp : Defines the exported functions for the DLL application.
//

#include "stdafx.h"


#include<boost/shared_ptr.hpp>
#include<CGAL/Exact_predicates_inexact_constructions_kernel.h>
#include<CGAL/Polygon_with_holes_2.h>
#include<CGAL/create_straight_skeleton_from_polygon_with_holes_2.h>

typedef CGAL::Exact_predicates_inexact_constructions_kernel K;

typedef K::Point_2                    Point;
typedef CGAL::Polygon_2<K>            Polygon_2;
typedef CGAL::Polygon_with_holes_2<K> Polygon_with_holes;
typedef CGAL::Straight_skeleton_2<K>  Ss;

typedef boost::shared_ptr<Ss> SsPtr;

typedef Ss::Vertex_const_handle		Vertex_const_handle;
typedef Ss::Halfedge_const_handle	Halfedge_const_handle;
typedef Ss::Halfedge_const_iterator	Halfedge_const_iterator;

struct Poly
{
	float* vertices;
	int verticesCount;
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

extern "C" __declspec(dllexport) void GenerateStraightSkeleton(Poly* outer, Poly* holes, int holesCount, Poly* straightSkeleton)
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
	straightSkeleton->verticesCount = ss.size_of_halfedges() * 2 * 2;	// edges * 2 (start and end) * 2 (X and Y)
	straightSkeleton->vertices = new float[straightSkeleton->verticesCount];

	//Copy vertex pairs
	int index = 0;
	for (Halfedge_const_iterator i = ss.halfedges_begin(); i != ss.halfedges_end(); ++i)
	{
		auto start = i->vertex()->point();
		auto end = i->next()->vertex()->point();

		straightSkeleton->vertices[index * 4] = start.x();
		straightSkeleton->vertices[index * 4 + 1] = start.y();
		straightSkeleton->vertices[index * 4 + 2] = end.x();
		straightSkeleton->vertices[index * 4 + 3] = end.y();

		index++;
	}
}

extern "C" __declspec(dllexport) void FreePolygonStructMembers(Poly* poly)
{
	delete poly->vertices;

	poly->vertices = nullptr;
	poly->verticesCount = 0;
}