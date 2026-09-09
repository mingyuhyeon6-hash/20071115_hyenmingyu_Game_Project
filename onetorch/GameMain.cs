// -------------------------------------------------------------------------------------------------------------------------------------------------------------
// Author: 3dapi (https://github.com/3dapi)
// -------------------------------------------------------------------------------------------------------------------------------------------------------------

using Vortice.Mathematics;

class GameMain : G2AppBase
{
	public override System.Drawing.Size ScreenSize => GameGlobal.ScreenSize;
	public override string GameName => GameGlobal.GameName;

	private G2Texture _bgTexture = null!;
    private G2Texture _torchTexture = null!;

	private G2Font _fntMessage = null!;	


    protected override void Initialize()
	{
		//---------------------------------------
		// 게임 관련 객체를 생성합니다.
		//---------------------------------------
		// 파일 경로/리소스 이름은 실제 값으로 변경
    	_bgTexture = new G2Texture("resource/background/map.png");
        _torchTexture = new G2Texture("resource/item/torch.png");

		_fntMessage = null;
    }

	protected override void Update()
	{
		double elapsed = TotalTime;

		this.ClearColor = new Color4(
			red: (float)(Math.Sin(elapsed) * 0.5 + 0.5),
			green: (float)(Math.Sin(elapsed + Math.PI / 2.0) * 0.5 + 0.5),
			blue: (float)(Math.Sin(elapsed + Math.PI) * 0.5 + 0.5),
			alpha: 1.0f);

		//---------------------------------------
		// 게임 관련 객체를 갱신합니다.
		//---------------------------------------
	}

	protected override void Render()
	{
		//---------------------------------------
		// 게임 관련 객체를 렌더링 합니다.
		//---------------------------------------
		_bgTexture?.Draw();
        _torchTexture?.Draw();
		_fntMessage?.Draw();
    }

	public override void Dispose()
	{
		base.Dispose();
		//---------------------------------------
		// 게임 관련 객체를 해제합니다.
		//---------------------------------------
		_bgTexture?.Dispose();
		_torchTexture?.Dispose();

    }
}
