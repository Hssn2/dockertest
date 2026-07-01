(function () {
    const canvas = document.getElementById('snake-canvas');
    if (!canvas) return;

    const ctx = canvas.getContext('2d');
    const scoreEl = document.getElementById('snake-score');
    const statusEl = document.getElementById('snake-status');
    const startBtn = document.getElementById('snake-start');

    const grid = 20;
    const tileCount = canvas.width / grid;

    let snake, direction, nextDirection, food, score, loopId, running;

    const colors = {
        bg: '#1a1d27',
        grid: '#252a36',
        snake: '#6c8cff',
        head: '#8aa4ff',
        food: '#4dd9c0'
    };

    function reset() {
        snake = [
            { x: 8, y: 10 },
            { x: 7, y: 10 },
            { x: 6, y: 10 }
        ];
        direction = { x: 1, y: 0 };
        nextDirection = { x: 1, y: 0 };
        food = spawnFood();
        score = 0;
        scoreEl.textContent = 'Skor: 0';
        statusEl.textContent = 'Oynuyor...';
    }

    function spawnFood() {
        let pos;
        do {
            pos = {
                x: Math.floor(Math.random() * tileCount),
                y: Math.floor(Math.random() * tileCount)
            };
        } while (snake.some(s => s.x === pos.x && s.y === pos.y));
        return pos;
    }

    function draw() {
        ctx.fillStyle = colors.bg;
        ctx.fillRect(0, 0, canvas.width, canvas.height);

        ctx.strokeStyle = colors.grid;
        ctx.lineWidth = 0.5;
        for (let i = 0; i <= tileCount; i++) {
            ctx.beginPath();
            ctx.moveTo(i * grid, 0);
            ctx.lineTo(i * grid, canvas.height);
            ctx.stroke();
            ctx.beginPath();
            ctx.moveTo(0, i * grid);
            ctx.lineTo(canvas.width, i * grid);
            ctx.stroke();
        }

        ctx.fillStyle = colors.food;
        ctx.beginPath();
        ctx.arc(food.x * grid + grid / 2, food.y * grid + grid / 2, grid / 2 - 2, 0, Math.PI * 2);
        ctx.fill();

        snake.forEach((seg, i) => {
            ctx.fillStyle = i === 0 ? colors.head : colors.snake;
            ctx.fillRect(seg.x * grid + 1, seg.y * grid + 1, grid - 2, grid - 2);
        });
    }

    function tick() {
        direction = nextDirection;
        const head = { x: snake[0].x + direction.x, y: snake[0].y + direction.y };

        if (head.x < 0 || head.y < 0 || head.x >= tileCount || head.y >= tileCount) {
            gameOver();
            return;
        }
        if (snake.some(s => s.x === head.x && s.y === head.y)) {
            gameOver();
            return;
        }

        snake.unshift(head);

        if (head.x === food.x && head.y === food.y) {
            score++;
            scoreEl.textContent = `Skor: ${score}`;
            food = spawnFood();
        } else {
            snake.pop();
        }

        draw();
    }

    function gameOver() {
        running = false;
        clearInterval(loopId);
        statusEl.textContent = `Oyun bitti! Skor: ${score}`;
    }

    function start() {
        if (running) clearInterval(loopId);
        reset();
        running = true;
        draw();
        loopId = setInterval(tick, 120);
        canvas.focus();
    }

    function setDirection(x, y) {
        if (!running) return;
        if (x === -direction.x && y === -direction.y) return;
        nextDirection = { x, y };
    }

    document.addEventListener('keydown', (e) => {
        if (!['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight'].includes(e.key)) return;
        e.preventDefault();
        if (e.key === 'ArrowUp') setDirection(0, -1);
        if (e.key === 'ArrowDown') setDirection(0, 1);
        if (e.key === 'ArrowLeft') setDirection(-1, 0);
        if (e.key === 'ArrowRight') setDirection(1, 0);
    });

    startBtn.addEventListener('click', start);
    canvas.addEventListener('click', () => canvas.focus());

    statusEl.textContent = 'Başla\'ya bas veya canvas\'a tıkla';
    draw();
})();
