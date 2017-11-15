namespace SpellerTask {

    class SpellerCell {
        
		public enum CellType : int {
			Empty,
            Input,
            Backspace,
			Exit
		};

        public int x;
        public int y;
        public int width;
        public int height;
        public CellType cellType;
        public string content;

        public SpellerCell(int x, int y, int width, int height, CellType type, string content) {
	        this.x = x;
	        this.y = y;
	        this.width = width;
	        this.height = height;
            cellType = type;
            this.content = content;
        }
    }
}
