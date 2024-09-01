using Microsoft.Xna.Framework;
using Netcode;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkestDepths.Labyrinth
{
    public class LabyrinthExit : INetObject<NetFields>
    {
        const string fieldCollectionName = "Labyrinth_Exit_Fields";
        NetPoint position = new NetPoint();
        NetString levelName = new NetString();

        public NetFields NetFields { get; } = new NetFields(fieldCollectionName);

        public LabyrinthExit()
        {
            Setup();
        }

        public LabyrinthExit(Point position, string levelName)
        {
            NetFields.SetOwner(this);
            this.position.Value = position;
            this.levelName.Value = levelName;

            Setup();
        }

        public Point Position
        {
            get { return position.Value; }
            set { position.Value = value; }
        }

        public string LevelName
        {
            get
            {
                return levelName.Value;
            }
            set
            {
                levelName.Value = value;
            }
        }

        private void Setup()
        {
            NetFields.AddField(levelName);
            NetFields.AddField(position);
        }
    }
}
