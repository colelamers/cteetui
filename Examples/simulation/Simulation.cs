using System.Collections.Generic;

namespace Tui.Sim
{
    // =========================================================================
    // CellState
    // Every cell on the simulation grid is one of these states.
    // =========================================================================
    public enum CellState
    {
        Dead   = 0,
        Ember  = 1,    // dying / fading particle
        AlphaA = 2,    // species A -- spreads fast, weak, needs food
        AlphaB = 3,    // species B -- spreads slow, strong, no food
        AlphaC = 4,    // species C -- random walker
        Food   = 5,    // static resource -- consumed by AlphaA
        Spark  = 6,    // brief bright flash on collision
        Decay  = 7,    // post-collision debris
    }

    // =========================================================================
    // SimEvent
    // Something notable happened that the log should capture.
    // =========================================================================
    public struct SimEvent
    {
        public string     Message;
        public Core.Color Color;
    }

    // =========================================================================
    // SimStats
    // Snapshot of current simulation metrics.
    // =========================================================================
    public class SimStats
    {
        public int   TickCount    = 0;
        public int   PopA         = 0;
        public int   PopB         = 0;
        public int   PopC         = 0;
        public int   FoodCount    = 0;
        public int   Sparks       = 0;
        public int   Births       = 0;      // this tick
        public int   Deaths       = 0;      // this tick
        public int   Collisions   = 0;      // this tick
        public float Entropy      = 0f;     // measure of grid disorder
        public int   PeakPop      = 0;
        public int   ExtinctCount = 0;
        public int   TotalBirths  = 0;
        public int   TotalDeaths  = 0;
    }

    // =========================================================================
    // Simulation
    // Double-buffered grid automaton.
    //
    // Rules:
    //   AlphaA  spreads to Dead neighbours when food is nearby; dies if starved
    //   AlphaB  conquers AlphaA cells (spark on collision); no food needed
    //   AlphaC  random-walks one step per tick; leaves Decay behind
    //   Food    static; consumed by adjacent AlphaA spread; spawns randomly
    //   Spark   lives exactly 1 tick then becomes Decay
    //   Decay   lives 2 ticks then becomes Dead
    //   Ember   lives 3 ticks then becomes Dead
    // =========================================================================
    public class Simulation
    {
        // =====================================================================
        // Private state
        // =====================================================================

        private CellState[]   grid_front_;
        private CellState[]   grid_back_;
        private int[]         age_;           // ticks this cell has been alive
        private int           width_;
        private int           height_;
        private System.Random rng_ = new System.Random();

        private Queue<SimEvent> pending_events_ = new Queue<SimEvent>();
        private SimStats        stats_          = new SimStats();

        // Extinction tracking
        private bool a_alive_last_ = true;
        private bool b_alive_last_ = true;
        private bool c_alive_last_ = true;

        // =====================================================================
        // Construction
        // =====================================================================

        public Simulation(int width, int height)
        {
            width_      = width;
            height_     = height;
            grid_front_ = new CellState[width * height];
            grid_back_  = new CellState[width * height];
            age_        = new int[width * height];

            seed_();
        }

        // =====================================================================
        // Public properties
        // =====================================================================

        public int      Width  { get { return width_;  } }
        public int      Height { get { return height_; } }
        public SimStats Stats  { get { return stats_;  } }

        // =====================================================================
        // Public methods
        // =====================================================================

        public CellState Get(int x, int y)
        {
            if (x < 0 || x >= width_ || y < 0 || y >= height_)
            {
                return CellState.Dead;
            }
            return grid_front_[y * width_ + x];
        }

        public int GetAge(int x, int y)
        {
            if (x < 0 || x >= width_ || y < 0 || y >= height_)
            {
                return 0;
            }
            return age_[y * width_ + x];
        }

        // ---------------------------------------------------------------------
        // Tick
        // Advance the simulation by one step.
        // ---------------------------------------------------------------------
        public void Tick()
        {
            stats_.TickCount++;
            stats_.Births     = 0;
            stats_.Deaths     = 0;
            stats_.Collisions = 0;

            // Occasionally spawn food
            if (rng_.Next(12) == 0)
            {
                spawn_food_();
            }

            // Periodically inject a new walker
            if (stats_.TickCount % 150 == 0)
            {
                inject_species_c_();
            }

            // Process each cell
            for (int y = 0; y < height_; y++)
            {
                for (int x = 0; x < width_; x++)
                {
                    process_cell_(x, y);
                }
            }

            // Swap buffers
            CellState[] tmp = grid_front_;
            grid_front_     = grid_back_;
            grid_back_      = tmp;

            // Age each live cell
            for (int i = 0; i < grid_front_.Length; i++)
            {
                if (grid_front_[i] != CellState.Dead)
                {
                    age_[i]++;
                }
                else
                {
                    age_[i] = 0;
                }
            }

            update_stats_();
            check_extinctions_();
            maybe_emit_event_();

            // Re-seed if the grid goes nearly dead
            int total_pop = stats_.PopA + stats_.PopB + stats_.PopC + stats_.FoodCount;
            if (total_pop < 8)
            {
                seed_();
                emit_event_("RESEED  Population collapse -- reseeding grid", Core.Color.Yellow);
            }
        }

        // ---------------------------------------------------------------------
        // DrainEvents
        // Return and clear all pending log events from this tick.
        // ---------------------------------------------------------------------
        public List<SimEvent> DrainEvents()
        {
            List<SimEvent> result = new List<SimEvent>();
            while (pending_events_.Count > 0)
            {
                result.Add(pending_events_.Dequeue());
            }
            return result;
        }

        // ---------------------------------------------------------------------
        // Reset
        // Wipe and re-seed the grid.
        // ---------------------------------------------------------------------
        public void Reset()
        {
            for (int i = 0; i < grid_front_.Length; i++)
            {
                grid_front_[i] = CellState.Dead;
                grid_back_[i]  = CellState.Dead;
                age_[i]        = 0;
            }
            stats_ = new SimStats();
            seed_();
            emit_event_("RESET   Grid cleared and reseeded", Core.Color.Cyan);
        }

        // =====================================================================
        // Private -- cell rules
        // =====================================================================

        private void process_cell_(int x, int y)
        {
            int       idx     = y * width_ + x;
            CellState current = grid_front_[idx];

            switch (current)
            {
                case CellState.AlphaA: rule_alpha_a_(x, y, idx); break;
                case CellState.AlphaB: rule_alpha_b_(x, y, idx); break;
                case CellState.AlphaC: rule_alpha_c_(x, y, idx); break;
                case CellState.Food:   rule_food_(x, y, idx);    break;
                case CellState.Spark:  rule_spark_(x, y, idx);   break;
                case CellState.Decay:  rule_decay_(x, y, idx);   break;
                case CellState.Ember:  rule_ember_(x, y, idx);   break;
                case CellState.Dead:   rule_dead_(x, y, idx);    break;
            }
        }

        // AlphaA -- fast spreader, needs food within 3 cells, dies if starved
        private void rule_alpha_a_(int x, int y, int idx)
        {
            bool has_food = food_nearby_(x, y, 3);

            if (has_food && rng_.Next(3) == 0)
            {
                int nx, ny;
                if (random_empty_neighbour_(x, y, out nx, out ny))
                {
                    set_back_(nx, ny, CellState.AlphaA);
                    consume_adjacent_food_(x, y);
                    stats_.Births++;
                }
            }

            if (!has_food && age_[idx] > 20)
            {
                set_back_(x, y, CellState.Ember);
                stats_.Deaths++;
            }
            else
            {
                set_back_(x, y, CellState.AlphaA);
            }
        }

        // AlphaB -- slow conqueror, turns AlphaA into Sparks, dies at old age
        private void rule_alpha_b_(int x, int y, int idx)
        {
            int[] dx = new int[] { -1, 1, 0, 0, -1, 1, -1, 1 };
            int[] dy = new int[] {  0, 0, -1, 1, -1, -1, 1, 1 };

            for (int d = 0; d < dx.Length; d++)
            {
                int nx = x + dx[d];
                int ny = y + dy[d];

                if (!in_bounds_(nx, ny))
                {
                    continue;
                }

                CellState ns = grid_front_[ny * width_ + nx];

                if (ns == CellState.AlphaA && rng_.Next(4) == 0)
                {
                    set_back_(nx, ny, CellState.Spark);
                    stats_.Collisions++;
                    stats_.Deaths++;
                }
                else if (ns == CellState.Dead && rng_.Next(8) == 0)
                {
                    set_back_(nx, ny, CellState.AlphaB);
                    stats_.Births++;
                }
            }

            if (age_[idx] > 80 && rng_.Next(6) == 0)
            {
                set_back_(x, y, CellState.Ember);
                stats_.Deaths++;
            }
            else
            {
                set_back_(x, y, CellState.AlphaB);
            }
        }

        // AlphaC -- random walker, leaves Decay trail
        private void rule_alpha_c_(int x, int y, int idx)
        {
            // Leave decay at current position
            set_back_(x, y, CellState.Decay);

            int[] dx = new int[] { -1, 1, 0, 0 };
            int[] dy = new int[] {  0, 0, -1, 1 };
            int   d  = rng_.Next(4);
            int   nx = x + dx[d];
            int   ny = y + dy[d];

            if (!in_bounds_(nx, ny))
            {
                set_back_(x, y, CellState.AlphaC);
                return;
            }

            CellState ns = grid_front_[ny * width_ + nx];

            if (ns == CellState.Dead || ns == CellState.Food || ns == CellState.Decay)
            {
                set_back_(nx, ny, CellState.AlphaC);
                if (ns == CellState.Food)
                {
                    stats_.Births++;
                }
            }
            else if (ns == CellState.AlphaA)
            {
                set_back_(nx, ny, CellState.Spark);
                set_back_(x, y, CellState.AlphaC);
                stats_.Collisions++;
            }
            else
            {
                set_back_(x, y, CellState.AlphaC);
            }
        }

        // Food -- static, just persists unless consumed
        private void rule_food_(int x, int y, int idx)
        {
            set_back_(x, y, CellState.Food);
        }

        // Spark -- lives 1 tick then becomes Decay
        private void rule_spark_(int x, int y, int idx)
        {
            set_back_(x, y, CellState.Decay);
        }

        // Decay -- lives 2 ticks
        private void rule_decay_(int x, int y, int idx)
        {
            if (age_[idx] >= 2)
            {
                set_back_(x, y, CellState.Dead);
            }
            else
            {
                set_back_(x, y, CellState.Decay);
            }
        }

        // Ember -- lives 3 ticks
        private void rule_ember_(int x, int y, int idx)
        {
            if (age_[idx] >= 3)
            {
                set_back_(x, y, CellState.Dead);
            }
            else
            {
                set_back_(x, y, CellState.Ember);
            }
        }

        // Dead -- very rare spontaneous food growth
        private void rule_dead_(int x, int y, int idx)
        {
            if (rng_.Next(800) == 0)
            {
                set_back_(x, y, CellState.Food);
            }
            else
            {
                set_back_(x, y, CellState.Dead);
            }
        }

        // =====================================================================
        // Private -- helpers
        // =====================================================================

        private void set_back_(int x, int y, CellState state)
        {
            if (!in_bounds_(x, y))
            {
                return;
            }
            grid_back_[y * width_ + x] = state;
        }

        private bool in_bounds_(int x, int y)
        {
            return x >= 0 && x < width_ && y >= 0 && y < height_;
        }

        private bool food_nearby_(int x, int y, int radius)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int nx = x + dx;
                    int ny = y + dy;

                    if (!in_bounds_(nx, ny))
                    {
                        continue;
                    }

                    if (grid_front_[ny * width_ + nx] == CellState.Food)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private bool random_empty_neighbour_(int x, int y, out int nx, out int ny)
        {
            int[] dx = new int[] { -1, 1, 0, 0, -1, 1, -1, 1 };
            int[] dy = new int[] {  0, 0, -1, 1, -1, -1, 1, 1 };

            // Shuffle direction array
            for (int i = dx.Length - 1; i > 0; i--)
            {
                int j   = rng_.Next(i + 1);
                int tmp = dx[i]; dx[i] = dx[j]; dx[j] = tmp;
                    tmp = dy[i]; dy[i] = dy[j]; dy[j] = tmp;
            }

            for (int d = 0; d < dx.Length; d++)
            {
                int cx = x + dx[d];
                int cy = y + dy[d];

                if (!in_bounds_(cx, cy))
                {
                    continue;
                }

                if (grid_front_[cy * width_ + cx] == CellState.Dead)
                {
                    nx = cx;
                    ny = cy;
                    return true;
                }
            }

            nx = x;
            ny = y;
            return false;
        }

        private void consume_adjacent_food_(int x, int y)
        {
            int[] dx = new int[] { -1, 1, 0, 0 };
            int[] dy = new int[] {  0, 0, -1, 1 };

            for (int d = 0; d < 4; d++)
            {
                int nx = x + dx[d];
                int ny = y + dy[d];

                if (!in_bounds_(nx, ny))
                {
                    continue;
                }

                if (grid_front_[ny * width_ + nx] == CellState.Food)
                {
                    set_back_(nx, ny, CellState.Dead);
                    return;
                }
            }
        }

        private void spawn_food_()
        {
            int x = rng_.Next(width_);
            int y = rng_.Next(height_);
            if (grid_front_[y * width_ + x] == CellState.Dead)
            {
                grid_front_[y * width_ + x] = CellState.Food;
            }
        }

        private void inject_species_c_()
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                int x = rng_.Next(width_);
                int y = rng_.Next(height_);
                if (grid_front_[y * width_ + x] == CellState.Dead)
                {
                    grid_front_[y * width_ + x] = CellState.AlphaC;
                    emit_event_("INJECT  New walker at (" + x + "," + y + ")", Core.Color.Cyan);
                    return;
                }
            }
        }

        private void seed_()
        {
            int cells = width_ * height_;

            // Scatter food
            for (int i = 0; i < cells / 8; i++)
            {
                int idx = rng_.Next(cells);
                grid_front_[idx] = CellState.Food;
            }

            // Seed species A in a central cluster
            int ax = rng_.Next(width_ / 4, width_ * 3 / 4);
            int ay = rng_.Next(height_ / 4, height_ * 3 / 4);
            for (int i = 0; i < 12; i++)
            {
                int x = ax + rng_.Next(-4, 5);
                int y = ay + rng_.Next(-4, 5);
                if (!in_bounds_(x, y))
                {
                    continue;
                }
                if (grid_front_[y * width_ + x] == CellState.Dead)
                {
                    grid_front_[y * width_ + x] = CellState.AlphaA;
                }
            }

            // Seed species B at the left edge
            int bx = rng_.Next(2, 6);
            int by = rng_.Next(2, height_ - 2);
            for (int i = 0; i < 5; i++)
            {
                int x = bx + rng_.Next(-2, 3);
                int y = by + rng_.Next(-2, 3);
                if (!in_bounds_(x, y))
                {
                    continue;
                }
                if (grid_front_[y * width_ + x] == CellState.Dead)
                {
                    grid_front_[y * width_ + x] = CellState.AlphaB;
                }
            }

            // Inject a starting walker
            inject_species_c_();
        }

        private void update_stats_()
        {
            int   pa     = 0;
            int   pb     = 0;
            int   pc     = 0;
            int   food   = 0;
            int   sparks = 0;
            float entropy = 0f;

            for (int i = 0; i < grid_front_.Length; i++)
            {
                switch (grid_front_[i])
                {
                    case CellState.AlphaA: pa++;     break;
                    case CellState.AlphaB: pb++;     break;
                    case CellState.AlphaC: pc++;     break;
                    case CellState.Food:   food++;   break;
                    case CellState.Spark:  sparks++; break;
                }
            }

            int occupied = pa + pb + pc + food + sparks;
            if (occupied > 0)
            {
                float ra = (float)pa     / occupied;
                float rb = (float)pb     / occupied;
                float rc = (float)pc     / occupied;
                float rf = (float)food   / occupied;
                float rs = (float)sparks / occupied;
                entropy = shannon_(ra) + shannon_(rb) + shannon_(rc)
                        + shannon_(rf) + shannon_(rs);
            }

            stats_.PopA        = pa;
            stats_.PopB        = pb;
            stats_.PopC        = pc;
            stats_.FoodCount   = food;
            stats_.Sparks      = sparks;
            stats_.Entropy     = entropy;
            stats_.TotalBirths += stats_.Births;
            stats_.TotalDeaths += stats_.Deaths;

            int total = pa + pb + pc;
            if (total > stats_.PeakPop)
            {
                stats_.PeakPop = total;
            }
        }

        private float shannon_(float p)
        {
            if (p <= 0f)
            {
                return 0f;
            }
            return -p * (float)System.Math.Log(p, 2.0);
        }

        private void check_extinctions_()
        {
            bool a_alive = stats_.PopA > 0;
            bool b_alive = stats_.PopB > 0;
            bool c_alive = stats_.PopC > 0;

            if (a_alive_last_ && !a_alive)
            {
                stats_.ExtinctCount++;
                emit_event_("EXTINCT Species-A wiped out at tick " + stats_.TickCount, Core.Color.Red);
            }

            if (b_alive_last_ && !b_alive)
            {
                stats_.ExtinctCount++;
                emit_event_("EXTINCT Species-B wiped out at tick " + stats_.TickCount, Core.Color.Red);
            }

            if (c_alive_last_ && !c_alive)
            {
                emit_event_("LOST    Walker lost at tick " + stats_.TickCount, Core.Color.Yellow);
            }

            if (!a_alive_last_ && a_alive)
            {
                emit_event_("REVIVE  Species-A re-emerged", Core.Color.Green);
            }

            if (!b_alive_last_ && b_alive)
            {
                emit_event_("REVIVE  Species-B re-emerged", Core.Color.Green);
            }

            a_alive_last_ = a_alive;
            b_alive_last_ = b_alive;
            c_alive_last_ = c_alive;
        }

        private void maybe_emit_event_()
        {
            if (stats_.TickCount % 500 == 0)
            {
                emit_event_(
                    "TICK    " + stats_.TickCount
                    + "  pop=" + (stats_.PopA + stats_.PopB + stats_.PopC)
                    + "  births=" + stats_.TotalBirths
                    + "  deaths=" + stats_.TotalDeaths,
                    Core.Color.Gray);
            }

            if (stats_.Births > 12)
            {
                emit_event_("BLOOM   Birth spike: " + stats_.Births + " this tick", Core.Color.Green);
            }

            if (stats_.Collisions > 6)
            {
                emit_event_("CLASH   " + stats_.Collisions + " collisions this tick", Core.Color.Magenta);
            }

            if (stats_.PopA + stats_.PopB + stats_.PopC == stats_.PeakPop
                && stats_.PeakPop > 0
                && stats_.TickCount % 100 == 1)
            {
                emit_event_("PEAK    Population record: " + stats_.PeakPop, Core.Color.Yellow);
            }
        }

        private void emit_event_(string msg, Core.Color color)
        {
            SimEvent ev;
            ev.Message = msg;
            ev.Color   = color;
            pending_events_.Enqueue(ev);
        }
    }
}
