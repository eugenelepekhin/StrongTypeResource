namespace StrongTypeResource {
	internal struct Parameter {
		public string Type { get; }
		public string Name { get; }
		public Parameter(string type, string name) {
			this.Type = type;
			this.Name = name;
		}
	}
}
