using FaceLookup.Common;
using FaceLookup.Service.Interfaces;
using HNSW.Net;
using Keras.Models;
using Keras.PreProcessing.Image;
using Numpy;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace FaceLookup.Service
{
    public class FacesIndex<T> where T : IFaceIndexsItem
    {
        public FacesIndex(string face2VectorModelPath, IFacesIndexDataProvider dataProvider)
        {
            _face2VectorModelPath = face2VectorModelPath;
            _dataProvider = dataProvider;
            _distanceFunction = CosineDistance.NonOptimized;
        }

        #region private data

        private CascadeClassifier _cascadeClassifier;
        private readonly IFacesIndexDataProvider _dataProvider;
        private readonly string _face2VectorModelPath;
        private readonly Func<float[], float[], float> _distanceFunction;
        private BaseModel _face2VectorModel;
        private SmallWorld<float[], float> _indexGraph;
        private int _indexVersion;
        private bool _isInited;

        #endregion

        public bool Init()
        {
            if (_isInited)
                return false;


            var parameters = new SmallWorld<float[], float>.Parameters()
            {
                //M = 15,
                //LevelLambda = 1 / Math.Log(15),             
            };            
            _indexGraph = new SmallWorld<float[], float>(_distanceFunction, DefaultRandomGenerator.Instance, parameters);
            var lastIndexInfo = _dataProvider.GetLastFacesIndex();
            if (lastIndexInfo.indexInfo != null && lastIndexInfo.latentVectors != null)
            {
                using (var ms = new MemoryStream(lastIndexInfo.indexInfo.Data, false))
                    _indexGraph = SmallWorld<float[], float>.DeserializeGraph(lastIndexInfo.latentVectors, _distanceFunction, DefaultRandomGenerator.Instance, ms);
                
                _indexVersion = lastIndexInfo.indexInfo.Version;
            }

            _face2VectorModel = Sequential.LoadModel(_face2VectorModelPath);

            _cascadeClassifier = new CascadeClassifier("haarcascade_frontalface_default.xml");

            _isInited = true;
            return true;
        }

        public ActionStatusEnum[] AddBulkFaces(IEnumerable<T> faces)
        {
            if (_isInited == false)
                throw new Exception("Init method will be called before");

            var report = new List<ActionStatusEnum>();

            var butchSize = 100;
            var batches = CollectionHelper.Split(faces, butchSize);

            var collection = new List<float[]>();
            var itemsCollection = new List<IFaceIndexsItem>();
            foreach (var batch in batches)
            {
                var images = batch.Select(info => Image.FromFile(info.FaceSource));

                var imageStatus = ActionStatusEnum.Success;
                var batchFaces = new List<Bitmap>();
                foreach (Bitmap image in images)
                {
                    var rects = DetectFaces(image);

                    if (rects.Length == 0)                    
                        imageStatus = ActionStatusEnum.FaceNotFound;                    

                    if (rects.Length > 1)                    
                        imageStatus = ActionStatusEnum.MoreThenOneFaceFound;

                    report.Add(imageStatus);

                    if (imageStatus != ActionStatusEnum.Success)
                        continue;

                    var corppedFace = image.Clone(new Rectangle(rects[0].Left, rects[0].Top, rects[0].Width, rects[0].Height), PixelFormat.Format24bppRgb);

                    batchFaces.Add(corppedFace);
                }

                if (!batchFaces.Any())
                    continue;

                var imagesArray = np.array(batchFaces.Select(x=>ReadImage(x)).ToArray());
                var imageLatentVector = _face2VectorModel.Predict(imagesArray);

                for (int i = 0; i < batch.Count; i++)
                {
                    var latentVector = imageLatentVector[i].GetData<float>();
                    var item = batch[i];
                    item.FaceVector = latentVector;

                    itemsCollection.Add(item);

                    collection.Add(latentVector);
                }
            }

            if (itemsCollection.Any())
            {
                // add to index
                var ids = _indexGraph.AddItems(collection);
                for (int i = 0; i < itemsCollection.Count; i++)
                {
                    var item = itemsCollection[i];
                    var id = ids[i];
                    item.FaceIndexId = id;
                }

                // update db
                using (var ms = new MemoryStream())
                {
                    _indexGraph.SerializeGraph(ms);
                    ms.Flush();
                    _indexVersion = _dataProvider.AddFaces(itemsCollection, ms.ToArray());
                }
            }

            return report.ToArray();
        }
        

        public FindFaceInfo[] FindFaces(Bitmap faceImage, int limit=1)
        {
            var imageNdArray = ReadImage(faceImage);
            var imageLatentVector = _face2VectorModel.Predict(np.array(new NDarray[] { imageNdArray }));
            var searchResults = _indexGraph.KNNSearch(imageLatentVector[0].GetData<float>(), limit);

            var resultList = new List<FindFaceInfo>();
            foreach (var result in searchResults)
            {
                var item = _dataProvider.GetFaceByIndexId(result.Id, _indexVersion);

                if (item != null)
                    resultList.Add(new FindFaceInfo { Distance = result.Distance, Face = item });
            }
            return resultList.OrderBy(x => x.Distance).ToArray();
        }

        #region private methods

        //private Image DetectAndCropFace(Bitmap image)
        //{
        //    var rects = DetectFaces(image);

        //    if (rects.Length == 0)
        //        throw new Exception("There is no face detected");

        //    if(rects.Length > 1)
        //        throw new Exception("More the one face face detected");

        //    var corppedFace = image.Clone(new Rectangle(rects[0].Left, rects[0].Top, rects[0].Width, rects[0].Height), PixelFormat.Format24bppRgb);

        //    return corppedFace;
        //}

        private Rect[] DetectFaces(Image image)
        {
            using(var ms = new MemoryStream())
            {
                image.Save(ms, ImageFormat.Jpeg);
                ms.Flush();
                ms.Position = 0;
                var img = Mat.FromStream(ms, ImreadModes.Color);
                return _cascadeClassifier.DetectMultiScale(img);
            }
        }

        private NDarray ReadImage(byte[] data)
        {
            //System.Drawing.Image.FromFile(inputPath)
            using (var ms = new MemoryStream(data, false))
            {
                var image = new Bitmap(ms);
                return ReadImage(image);
            }
            return null;
        }

        private NDarray ReadImage(Bitmap image)
        {
            var resizedImage = ImageHelper.ResizeImage(image, 64, 64);

            var imageData = resizedImage.LockBits(new Rectangle(new System.Drawing.Point(), resizedImage.Size), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            var byteArray = new byte[resizedImage.Width * resizedImage.Height * 3];
            Marshal.Copy(imageData.Scan0, byteArray, 0, byteArray.Length);
            var npArray = np.array(byteArray.Select(x => Convert.ToSingle(x)).ToArray());
            var result = npArray.reshape(resizedImage.Width, resizedImage.Height, 3)/255F;

            return result;
        }

        private NDarray ReadImage(string imagePath)
        {
            var image = ImageUtil.LoadImg(imagePath, color_mode: "rgb", target_size: new Keras.Shape(64, 64));
            var imageArray = (ImageUtil.ImageToArray(image) as NDarray) / 255F;
            return imageArray.reshape(64, 64, 3);
        }

        #endregion
    }
}
