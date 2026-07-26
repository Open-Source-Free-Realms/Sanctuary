using System.Collections.Generic;
using System.Numerics;

using Microsoft.Extensions.Logging;

namespace Sanctuary.Game.Pathfinding;

public class Pathfinder<TNode> where TNode : IPathNode
{
    private readonly IReadOnlyDictionary<int, TNode> _nodes;
    private readonly ILogger _logger;

    public Pathfinder(IReadOnlyDictionary<int, TNode> nodes, ILogger logger)
    {
        this._nodes = nodes;
        this._logger = logger;
    }

    public List<TNode> FindPath(Vector3 startPosition, Vector3 goalPosition) {
        var startNode = this.FindClosestNode(startPosition);
        var goalNode = this.FindClosestNode(goalPosition);

        _logger.LogDebug(
            "Finding path from {StartPosition} to {GoalPosition} (start node {StartNodeId}, goal node {GoalNodeId})...",
            startPosition, goalPosition, startNode.Id, goalNode.Id);

        var bestCost = float.PositiveInfinity;
        int? meetingNodeId = null;


        // NOTE: this was ported from a Python implementation that I (Alko) have.
        // Apparently, `heapq` and PriorityQueue have difference tie breaker 
        // behavior. This shouldn't affect the validity of the solution, but if
        // anyone ever gets their hands on the Python version for validation, it may 
        // not be 1:1.
        var forwardQueue = new PriorityQueue<int, float>();
        var forwardGScores = new Dictionary<int, float>();
        var forwardCameFrom = new Dictionary<int, int>();
        var forwardClosedNodes = new HashSet<int>();

        var backwardQueue = new PriorityQueue<int, float>();
        var backwardGScores = new Dictionary<int, float>();
        var backwardCameFrom = new Dictionary<int, int>();
        var backwardClosedNodes = new HashSet<int>();

        forwardGScores[startNode.Id] = 0f;
        forwardQueue.Enqueue(startNode.Id, Heuristic(startNode, goalNode));

        backwardGScores[goalNode.Id] = 0f;
        backwardQueue.Enqueue(goalNode.Id, Heuristic(goalNode, startNode));

        while (forwardQueue.Count > 0 && backwardQueue.Count > 0)
        {
            // Once we get a meeting candidate, we check if the total cost
            // of our expansion could produce a better path. If so, we
            // continue and find a new meeting candidate.
            if (!forwardQueue.TryPeek(out _, out var forwardFScore))
                break;

            if (!backwardQueue.TryPeek(out _, out var backwardFScore))
                break;

            if (forwardFScore + backwardFScore >= bestCost)
                break;

            // We'll start by expanding the smaller cost first. Not sure if this
            // will really improve efficiency that much in practice.
            (float Cost, int NodeId)? meetingCandidate;
            if (forwardFScore <= backwardFScore) 
            {
                meetingCandidate = this.Expand(
                    forwardQueue,
                    forwardClosedNodes,
                    forwardGScores,
                    backwardGScores,
                    forwardCameFrom,
                    goalNode
                );
            } 
            else 
            {
                meetingCandidate = this.Expand(
                    backwardQueue,
                    backwardClosedNodes,
                    backwardGScores,
                    forwardGScores,
                    backwardCameFrom,
                    startNode
                );
            }

            if (meetingCandidate is not null)
            {
                var (meetingCandidateCost, meetingCandidateId) = meetingCandidate.Value;

                if (meetingCandidateCost < bestCost)
                {
                    bestCost = meetingCandidateCost;
                    meetingNodeId = meetingCandidateId;
                }
            }
        }

        if (meetingNodeId is null) {
            _logger.LogWarning(
                "No path found between node {StartNodeId} and node {GoalNodeId}.",
                startNode.Id, goalNode.Id);
            return new List<TNode>();
        }

        var path = this.ReconstructPath(
            startNode,
            goalNode,
            this._nodes[meetingNodeId.Value],
            forwardCameFrom,
            backwardCameFrom
        );

        _logger.LogInformation(
            "Path found: {NodeCount} nodes, cost {Cost:F1}.",
            path.Count, bestCost);
        return path; 
    }

    private TNode FindClosestNode(Vector3 position) 
    {
        // TODO: This is linear and probably fine for small graphs, but if we ever do
        // things with large navmesh graphs, we might need to make this more efficient!

        var minDistSquared = float.PositiveInfinity;
        var closestId = 0;

        foreach (var (id, node) in this._nodes)
            {
                var difference = position - node.Position;
                var distSquared = difference.LengthSquared();
                if (distSquared < minDistSquared)
                {
                    closestId = id;
                    minDistSquared = distSquared;
                }
            }

        return this._nodes[closestId]; 
    }

    private (float Cost, int NodeId)? Expand(
        PriorityQueue<int, float> queue,
        HashSet<int> closedNodes,
        Dictionary<int, float> gScores,
        Dictionary<int, float> otherGScores,
        Dictionary<int, int> cameFrom,
        TNode targetNode)
    {
        if (!queue.TryDequeue(out var currentNodeId, out _))
            return null;

        if (closedNodes.Contains(currentNodeId))
            return null;

        closedNodes.Add(currentNodeId);

        (float Cost, int NodeId)? meetingCandidate = null;
        if (otherGScores.TryGetValue(currentNodeId, out var otherGScore))
        {
            var combinedGScore = gScores[currentNodeId] + otherGScore;
            meetingCandidate = (combinedGScore, currentNodeId);
        }

        var currentNode = _nodes[currentNodeId];
        foreach (var (neighborNodeId, distance) in currentNode.Neighbors)
        {
            var gScore = gScores[currentNodeId] + distance;

            if (gScore < gScores.GetValueOrDefault(neighborNodeId, float.PositiveInfinity))
            {
                var neighborNode = _nodes[neighborNodeId];
                cameFrom[neighborNodeId] = currentNodeId;
                gScores[neighborNodeId] = gScore;

                var fScore = gScore + Heuristic(neighborNode, targetNode);

                queue.Enqueue(neighborNodeId, fScore);
            }
        }

        return meetingCandidate;
    }
    
    private List<TNode> ReconstructPath(
        TNode startNode,
        TNode goalNode,
        TNode meetingNode,
        Dictionary<int, int> forwardCameFrom,
        Dictionary<int, int> backwardCameFrom)
    {
        var forwardPath = WalkChain(forwardCameFrom, meetingNode.Id, startNode.Id);
        forwardPath.Reverse();

        var backwardPath = WalkChain(backwardCameFrom, meetingNode.Id, goalNode.Id);

        var path = new List<TNode>(forwardPath);

        // NOTE: We don't want to double count the meeting node here.
        path.AddRange(backwardPath.GetRange(1, backwardPath.Count - 1));

        return path;
    }

    private List<TNode> WalkChain(Dictionary<int, int> cameFrom, int fromId, int toId)
    {
        var path = new List<TNode> { _nodes[fromId] };

        var currentId = fromId;
        while (currentId != toId)
        {
            currentId = cameFrom[currentId];
            path.Add(_nodes[currentId]);
        }

        return path;
    }

    private float Heuristic(TNode node, TNode goalNode)
    {
        return Vector3.Distance(node.Position, goalNode.Position);
    }
}