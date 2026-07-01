(function () {
    const board = document.getElementById('xox-board');
    if (!board) return;

    const statusEl = document.getElementById('xox-status');
    const resetBtn = document.getElementById('xox-reset');
    const wins = [
        [0, 1, 2], [3, 4, 5], [6, 7, 8],
        [0, 3, 6], [1, 4, 7], [2, 5, 8],
        [0, 4, 8], [2, 4, 6]
    ];

    let cells = Array(9).fill('');
    let current = 'X';
    let gameOver = false;

    function render() {
        board.querySelectorAll('.xox-cell').forEach((btn, i) => {
            btn.textContent = cells[i];
            btn.classList.remove('x', 'o');
            if (cells[i] === 'X') btn.classList.add('x');
            if (cells[i] === 'O') btn.classList.add('o');
            btn.disabled = gameOver || cells[i] !== '';
        });
    }

    function checkWinner() {
        for (const [a, b, c] of wins) {
            if (cells[a] && cells[a] === cells[b] && cells[b] === cells[c]) {
                return cells[a];
            }
        }
        if (cells.every(c => c)) return 'draw';
        return null;
    }

    function updateStatus() {
        const result = checkWinner();
        if (result === 'X' || result === 'O') {
            statusEl.textContent = `${result} kazandı!`;
            gameOver = true;
        } else if (result === 'draw') {
            statusEl.textContent = 'Berabere!';
            gameOver = true;
        } else {
            statusEl.textContent = `Sıra: ${current}`;
        }
        render();
    }

    board.addEventListener('click', (e) => {
        const btn = e.target.closest('.xox-cell');
        if (!btn || gameOver) return;
        const index = parseInt(btn.dataset.index, 10);
        if (cells[index]) return;
        cells[index] = current;
        current = current === 'X' ? 'O' : 'X';
        updateStatus();
    });

    resetBtn.addEventListener('click', () => {
        cells = Array(9).fill('');
        current = 'X';
        gameOver = false;
        statusEl.textContent = 'Sıra: X';
        render();
    });

    render();
})();
