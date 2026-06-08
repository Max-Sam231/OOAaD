import pandas as pd
import numpy as np

def generate_collision_matrix(dx_range, dy_range, ddx_range, ddy_range, radius):
    collisions = []
    R2 = radius ** 2

    for ddx in ddx_range:
        for ddy in ddy_range:
            A = ddx**2 + ddy**2
            
            for dx in dx_range:
                for dy in dy_range:
                    C = dx**2 + dy**2
                    
                    if A == 0:
                        if C <= R2:
                            collisions.append((dx, dy, ddx, ddy))
                    else:
                        B = 2 * (dx * ddx + dy * ddy)

                        t_min = -B / (2 * A)

                        if t_min <= 0:
                            D2 = C
                        elif t_min >= 1:
                            D2 = A + B + C
                        else:
                            D2 = C - (B**2) / (4 * A)

                        if D2 <= R2:
                            collisions.append((dx, dy, ddx, ddy))

    df = pd.DataFrame(collisions, columns=['dx', 'dy', 'ddx', 'ddy'])
    return df

dx_range = np.arange(-20, 21)
dy_range = np.arange(-20, 21)
ddx_range = np.arange(-8, 9)
ddy_range = np.arange(-8, 9)

df_collisions = generate_collision_matrix(dx_range, dy_range, ddx_range, ddy_range, radius=2.5)

print(len(df_collisions))
df_collisions.to_csv("collision_matrix.csv", index=False)