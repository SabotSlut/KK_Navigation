using System;
using System.Collections;

namespace EMK.Cartography
{
	/// <summary>
	/// Basically a node is defined with a geographical position in space.
	/// It is also characterized with both collections of outgoing arcs and incoming arcs.
	/// </summary>
	[Serializable]
	public class Node
	{
		bool _Passable;
		ArrayList _IncomingArcs, _OutgoingArcs;
        public readonly int ID;

		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="id">The ID of this node.</param>
		public Node(int id)
		{
            ID = id;
			_Passable = true;
			_IncomingArcs = new ArrayList();
			_OutgoingArcs = new ArrayList();
		}

		/// <summary>
		/// Gets the list of the arcs that lead to this node.
		/// </summary>
		public IList IncomingArcs => _IncomingArcs;

        /// <summary>
		/// Gets the list of the arcs that start from this node.
		/// </summary>
		public IList OutgoingArcs => _OutgoingArcs;

        /// Gets/Sets the functional state of the node.
		/// 'true' means that the node is in its normal state.
		/// 'false' means that the node will not be taken into account (as if it did not exist).
		public bool Passable
		{
			set
			{
				foreach (Arc A in _IncomingArcs) A.Passable = value;
				foreach (Arc A in _OutgoingArcs) A.Passable = value;
				_Passable = value;
			}
			get => _Passable;
        }

		/// <summary>
		/// Gets the array of nodes that can be directly reached from this one.
		/// </summary>
		public Node[] AccessibleNodes
		{
			get
			{
				Node[] Tableau = new Node[_OutgoingArcs.Count];
				int i=0;
				foreach (Arc A in OutgoingArcs) Tableau[i++] = A.EndNode;
				return Tableau;
			}
		}

		/// <summary>
		/// Gets the array of nodes that can directly reach this one.
		/// </summary>
		public Node[] AccessingNodes
		{
			get
			{
				Node[] Tableau = new Node[_IncomingArcs.Count];
				int i=0;
				foreach (Arc A in IncomingArcs) Tableau[i++] = A.StartNode;
				return Tableau;
			}
		}
		
		/// <summary>
		/// Gets the array of nodes directly linked plus this one.
		/// </summary>
		public Node[] Molecule
		{
			get
			{
				int NbNodes = 1+_OutgoingArcs.Count+_IncomingArcs.Count;
				Node[] Tableau = new Node[NbNodes];
				Tableau[0] = this;
				int i=1;
				foreach (Arc A in OutgoingArcs) Tableau[i++] = A.EndNode;
				foreach (Arc A in IncomingArcs) Tableau[i++] = A.StartNode;
				return Tableau;
			}
		}
		
		/// <summary>
		/// Unlink this node from all current connected arcs.
		/// </summary>
		public void Isolate()
		{
			UntieIncomingArcs();
			UntieOutgoingArcs();
		}

		/// <summary>
		/// Unlink this node from all current incoming arcs.
		/// </summary>
		public void UntieIncomingArcs()
		{
			foreach (Arc A in _IncomingArcs)
				A.StartNode.OutgoingArcs.Remove(A);
			_IncomingArcs.Clear();
		}

		/// <summary>
		/// Unlink this node from all current outgoing arcs.
		/// </summary>
		public void UntieOutgoingArcs()
		{
			foreach (Arc A in _OutgoingArcs)
				A.EndNode.IncomingArcs.Remove(A);
			_OutgoingArcs.Clear();
		}

		/// <summary>
		/// Returns the arc that leads to the specified node if it exists.
		/// </summary>
		/// <exception cref="ArgumentNullException">Argument node must not be null.</exception>
		/// <param name="N">A node that could be reached from this one.</param>
		/// <returns>The arc leading to N from this / null if there is no solution.</returns>
		public Arc ArcGoingTo(Node N)
		{
			if ( N==null ) throw new ArgumentNullException();
			foreach (Arc A in _OutgoingArcs)
				if (A.EndNode == N) return A;
			return null;
		}

		/// <summary>
		/// Returns the arc that arc that comes to this from the specified node if it exists.
		/// </summary>
		/// <exception cref="ArgumentNullException">Argument node must not be null.</exception>
		/// <param name="N">A node that could reach this one.</param>
		/// <returns>The arc coming to this from N / null if there is no solution.</returns>
		public Arc ArcComingFrom(Node N)
		{
			if ( N==null ) throw new ArgumentNullException();
			foreach (Arc A in _IncomingArcs)
				if (A.StartNode == N) return A;
			return null;
		}
		
		void Invalidate()
		{
			foreach (Arc A in _IncomingArcs) A.LengthUpdated = false;
			foreach (Arc A in _OutgoingArcs) A.LengthUpdated = false;
		}

		/// <summary>
		/// object.ToString() override.
		/// Returns the textual description of the node.
		/// </summary>
		/// <returns>String describing this node.</returns>
		public override string ToString() { return ID.ToString(); }

		/// <summary>
		/// Object.Equals override.
		/// Tells if two nodes are equal by comparing positions.
		/// </summary>
		/// <exception cref="ArgumentException">A Node cannot be compared with another type.</exception>
		/// <param name="O">The node to compare with.</param>
		/// <returns>'true' if both nodes are equal.</returns>
		public override bool Equals(object O)
		{
			Node N = (Node)O;
			if ( N==null ) throw new ArgumentException("Type "+O.GetType()+" cannot be compared with type "+GetType()+"!");
			return ID == N.ID;
		}

		/// <summary>
		/// Returns a copy of this node.
		/// </summary>
		/// <returns>The reference of the new object.</returns>
		public object Clone()
		{
			Node N = new Node(ID);
			N._Passable = _Passable;
			return N;
		}

		/// <summary>
		/// Object.GetHashCode override.
		/// </summary>
		/// <returns>HashCode value.</returns>
		public override int GetHashCode() { return ID.GetHashCode(); }

        /// <summary>
        /// Returns the assigned distance between two nodes : Dx²+Dy²+Dz²
        /// </summary>
        /// <exception cref="ArgumentNullException">Argument nodes must not be null.</exception>
        /// <param name="N1">First node.</param>
        /// <param name="N2">Second node.</param>
        /// <returns>Distance value.</returns>
        public static double AssignedDistance(Node N1, Node N2)
        {
            if (N1 == null || N2 == null) throw new ArgumentNullException();
            var arc = N1.ArcComingFrom(N2);
			if (arc == null)
            {
                return 0;
            }

            return arc.Distance;
        }
	}
}

