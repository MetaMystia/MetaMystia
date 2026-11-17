using MetaMystia;
using MetaMystia.Network;
using MetaMystia.UI;

static class PlayerPresenceChecks
{
    public static void Run(Action<bool, string> check)
    {
        var self = new Player { Uid = 1, Name = "self", Membership = 10 };
        var peer = new Player { Uid = 2, Name = "peer", Membership = 20 };
        var stranger = new Player { Uid = 3, Name = "stranger", Membership = 30 };
        var room = new Room { Id = 0xABCD, Host = 1, Members = [self, peer] };
        var state = new Snapshot { World = [self, peer], Room = room, Rooms = [new() { Id = room.Id }] };
        void Change(Snapshot before, Snapshot after)
        {
            InGameConsole.Messages.Clear();
            PlayerPresenceNotice.ShowChanges(before, after, self.Uid);
        }
        bool Messages(params string[] expected) => InGameConsole.Messages.SequenceEqual(expected);

        Change(new(), state);
        check(Messages(), "首次同步不把已有在线玩家或房间成员报为新加入");
        Change(state, state.Copy());
        check(Messages(), "重复快照不产生进出提示");
        Change(state, state with { World = [self, peer with { Name = "renamed", Motion = new() { X = 5 } }] });
        check(Messages(), "改名和移动不产生进出提示");

        var online = state with { World = [self, peer, stranger] };
        Change(state, online);
        check(Messages("PeerConnected: stranger"), "其他玩家上线仅提示进入服务器");
        Change(online, online with { Rooms = [new() { Id = room.Id }, new() { Id = 2 }] });
        check(Messages(), "其他房间变化不产生本房间进出提示");
        var joined = online with { Room = room with { Members = [self, peer, stranger] } };
        Change(online, joined);
        check(Messages("PeerJoined: stranger"), "同房间新成员只提示一次加入");
        Change(state with { Room = null }, state);
        check(Messages(), "本人入房不把原有成员误报为新加入");

        Change(joined, online);
        check(Messages("PeerLeft: stranger"), "退房但未断线只显示退房");
        Change(joined, state);
        check(Messages("PeerLeft: stranger", "PeerDisconnected: stranger"), "房间成员掉线依次显示退房和离开服务器");
        Change(online, state);
        check(Messages("PeerDisconnected: stranger"), "大厅玩家掉线只显示离开服务器");
        Change(state, state with { Room = room with { Members = [self, peer with { Membership = 21 }] } });
        check(Messages("PeerLeft: peer", "PeerJoined: peer"), "相同 UID 重新入房按入房代号识别离开与加入");
        Change(state, state with { Room = null });
        check(Messages("NetworkRoomLeft: ABCD"), "本人离开仍存在的房间不误报其他成员离开");
        Change(state, state with { Room = null, Rooms = [] });
        check(Messages("PeerLeft: peer", "NetworkRoomLeft: ABCD"), "房间解散提示原成员离开和本人退房");

        var named = state with { World = [self, peer, stranger with { Name = "<b>name</b>" }] };
        Change(state, named);
        check(Messages("PeerConnected: ＜b＞name＜/b＞"), "玩家名字不能注入控制台富文本");
        LiveModeManager.IsActive = true;
        Change(state, named);
        check(Messages("PeerConnected: UID-3"), "直播模式提示使用 UID 不暴露玩家名字");
        LiveModeManager.IsActive = false;
    }
}
